#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Panes;

internal class CompletionPane : IKeyPressHandler
{
    /// <summary>
    /// Left padding + right padding + right border.
    /// </summary>
    public const int HorizontalBordersWidth = 3;

    /// <summary>
    /// Top border + bottom border.
    /// </summary>
    public const int VerticalBordersHeight = 2;

    /// <summary>
    /// Cursor + top border + bottom border.
    /// </summary>
    private const int VerticalPaddingHeight = 1 + VerticalBordersHeight;

    private readonly CodePane codePane;
    private readonly CompletionCallbackAsync complete;
    private readonly OpenCompletionWindowCallbackAsync? shouldOpenCompletionWindow;
    private readonly int minCompletionItemsCount;
    private readonly int maxCompletionItemsCount;

    /// <summary>
    /// The index of the caret when the pane was opened
    /// </summary>
    private int openedCaretIndex;

    /// <summary>
    /// All completions available. Called once when the window is initially opened
    /// </summary>
    private IReadOnlyList<CompletionItem> allCompletions = Array.Empty<CompletionItem>();

    /// <summary>
    /// An "ordered view" over <see cref="allCompletions"/> that shows the list filtered by what the user has typed.
    /// </summary>
    public SlidingArrayWindow<CompletionItem> FilteredView { get; set; } = new SlidingArrayWindow<CompletionItem>();

    /// <summary>
    /// Whether or not the window is currently open / visible.
    /// </summary>
    public bool IsOpen { get; set; }

    public CompletionPane(
        CodePane codePane,
        CompletionCallbackAsync complete,
        OpenCompletionWindowCallbackAsync? shouldOpenCompletionWindow,
        int minCompletionItemsCount,
        int maxCompletionItemsCount)
    {
        this.codePane = codePane;
        this.complete = complete;
        this.shouldOpenCompletionWindow = shouldOpenCompletionWindow;
        this.minCompletionItemsCount = minCompletionItemsCount;
        this.maxCompletionItemsCount = maxCompletionItemsCount;
    }

    private void Open(int caret)
    {
        this.IsOpen = true;
        this.openedCaretIndex = caret;
        this.allCompletions = Array.Empty<CompletionItem>();
    }

    private void Close()
    {
        this.IsOpen = false;
        this.openedCaretIndex = int.MinValue;
        this.FilteredView = new SlidingArrayWindow<CompletionItem>();
    }

    Task IKeyPressHandler.OnKeyDown(KeyPress key)
    {
        if (!EnoughRoomToDisplay(this.codePane)) return Task.CompletedTask;

        if (!IsOpen)
        {
            if (key.Pattern is (Control, Spacebar))
            {
                Open(codePane.Document.Caret);
                key.Handled = true;
                return Task.CompletedTask;
            }
            key.Handled = false;
            return Task.CompletedTask;
        }

        if (FilteredView is null || FilteredView.Count == 0)
        {
            key.Handled = false;
            return Task.CompletedTask;
        }

        switch (key.Pattern)
        {
            case DownArrow:
                this.FilteredView.IncrementSelectedIndex();
                key.Handled = true;
                break;
            case UpArrow:
                this.FilteredView.DecrementSelectedIndex();
                key.Handled = true;
                break;
            case Enter:
            case RightArrow:
            case Tab:
            case (Control, Spacebar) when FilteredView.Count == 1:
                Debug.Assert(!FilteredView.IsEmpty);
                InsertCompletion(codePane.Document, FilteredView.SelectedItem);
                key.Handled = true;
                break;
            case (Control, Spacebar):
                key.Handled = true;
                break;
            case Home or (_, Home):
            case End or (_, End):
            case (Shift, LeftArrow or RightArrow or UpArrow or DownArrow or Home or End)
                 or (Control | Shift, LeftArrow or RightArrow or UpArrow or DownArrow or Home or End):
            case LeftArrow:
                Close();
                key.Handled = false;
                break;
            case Escape:
                Close();
                key.Handled = true;
                break;
            default:
                this.FilteredView.ResetSelectedIndex();
                key.Handled = false;
                break;
        }

        return Task.CompletedTask;
    }

    private bool EnoughRoomToDisplay(CodePane codePane) =>
        codePane.CodeAreaHeight - codePane.Cursor.Row >= VerticalPaddingHeight + minCompletionItemsCount; // offset + top border + MinCompletionItemsCount + bottom border

    async Task IKeyPressHandler.OnKeyUp(KeyPress key)
    {
        if (!EnoughRoomToDisplay(this.codePane)) return;

        if (!char.IsControl(key.ConsoleKeyInfo.KeyChar)
            && (await ShouldAutomaticallyOpen(codePane.Document, codePane.Document.Caret)) is int offset and >= 0)
        {
            Close();
            Open(codePane.Document.Caret - offset);
        }

        if (codePane.Document.Caret < openedCaretIndex)
        {
            Close();
        }
        else if (IsOpen)
        {
            if (allCompletions.Count == 0)
            {
                var completions = await this.complete.Invoke(codePane.Document.GetText(), codePane.Document.Caret).ConfigureAwait(false);
                if (completions.Any())
                {
                    SetCompletions(completions, codePane);
                }
                else
                {
                    Close();
                }
            }
            else if (!key.Handled)
            {
                FilterCompletions(codePane);
                if (HasTypedPastCompletion() || ShouldCancelOpenMenu(key))
                {
                    Close();
                }
            }
        }
    }

    private bool HasTypedPastCompletion() =>
        !FilteredView.IsEmpty
        && FilteredView.SelectedItem.ReplacementText.Length < (codePane.Document.Caret - openedCaretIndex);

    private static bool ShouldCancelOpenMenu(KeyPress key) =>
        key.Pattern is LeftArrow or (_, LeftArrow);

    private void SetCompletions(IReadOnlyList<CompletionItem> completions, CodePane codePane)
    {
        allCompletions = completions;
        if (completions.Any())
        {
            openedCaretIndex = completions[0].StartIndex;
            FilterCompletions(codePane);
        }
    }

    private void FilterCompletions(CodePane codePane)
    {
        int height = Math.Min(codePane.CodeAreaHeight - VerticalPaddingHeight, maxCompletionItemsCount);

        var filtered = new List<CompletionItem>();
        var previouslySelectedItem = this.FilteredView.SelectedItem;
        int selectedIndex = -1;
        for (var i = 0; i < allCompletions.Count; i++)
        {
            var completion = allCompletions[i];
            if (!Matches(completion, codePane.Document)) continue;

            filtered.Add(completion);
            if (completion.ReplacementText == previouslySelectedItem?.ReplacementText)
            {
                selectedIndex = filtered.Count - 1;
            }
        }
        if (selectedIndex == -1 || !Matches(previouslySelectedItem, codePane.Document))
        {
            selectedIndex = 0;
        }
        FilteredView = new SlidingArrayWindow<CompletionItem>(
            filtered.ToArray(),
            height,
            selectedIndex
        );

        bool Matches(CompletionItem? completion, Document input) =>
            completion?.ReplacementText.StartsWith(
                input.GetText(completion.StartIndex, codePane.Document.Caret - completion.StartIndex).Trim(),
                StringComparison.CurrentCultureIgnoreCase
            ) ?? false;
    }

    private Task<int> ShouldAutomaticallyOpen(Document input, int caret)
    {
        if (shouldOpenCompletionWindow is not null)
        {
            return shouldOpenCompletionWindow(input.GetText(), caret);
        }

        if (caret > 0 && input[caret - 1] is '.' or '(') // typical "intellisense behavior", opens for new methods and parameters
        {
            return Task.FromResult(0);
        }

        if (caret == 1 && !char.IsWhiteSpace(input[0]) // 1 word character typed in brand new prompt
            && (input.Length == 1 || !char.IsLetterOrDigit(input[1]))) // if there's more than one character on the prompt, but we're typing a new word at the beginning (e.g. "a| bar")
        {
            return Task.FromResult(1);
        }

        // open when we're starting a new "word" in the prompt.
        return caret - 2 >= 0
            && char.IsWhiteSpace(input[caret - 2])
            && char.IsLetter(input[caret - 1])
            ? Task.FromResult(1)
            : Task.FromResult(-1);
    }

    private void InsertCompletion(Document input, CompletionItem completion, string suffix = "")
    {
        input.Remove(completion.StartIndex, input.Caret - completion.StartIndex);
        input.InsertAtCaret(completion.ReplacementText + suffix, codePane.GetSelectionStartEnd());
        input.Caret = completion.StartIndex + completion.ReplacementText.Length + suffix.Length;
        Close();
    }
}
