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
    private readonly IPromptCallbacks promptCallbacks;
    private readonly PromptConfiguration configuration;

    private TextSpan? lastSpanToReplaceOnKeyDown;

    /// <summary>
    /// All completions available. Called once when the window is initially opened
    /// </summary>
    private IReadOnlyList<CompletionItem> allCompletions = Array.Empty<CompletionItem>();

    /// <summary>
    /// An "ordered view" over <see cref="allCompletions"/> that shows the list filtered by what the user has typed.
    /// </summary>
    public SlidingArrayWindow FilteredView { get; set; } = new SlidingArrayWindow();

    /// <summary>
    /// Whether or not the window is currently open / visible.
    /// </summary>
    public bool IsOpen { get; set; }

    public CompletionPane(
        CodePane codePane,
        IPromptCallbacks promptCallbacks,
        PromptConfiguration configuration)
    {
        this.codePane = codePane;
        this.promptCallbacks = promptCallbacks;
        this.configuration = configuration;
    }

    private void Open()
    {
        this.IsOpen = true;
        this.allCompletions = Array.Empty<CompletionItem>();
    }

    private void Close()
    {
        this.IsOpen = false;
        this.FilteredView.Clear();
    }

    async Task IKeyPressHandler.OnKeyDown(KeyPress key)
    {
        if (!EnoughRoomToDisplay(this.codePane)) return;

        var completionListTriggered = configuration.KeyBindings.TriggerCompletionList.Matches(key.ConsoleKeyInfo);
        lastSpanToReplaceOnKeyDown = null;

        if (IsOpen)
        {
            var documentText = codePane.Document.GetText();
            int documentCaret = codePane.Document.Caret;
            var spanToReplace = await promptCallbacks.GetSpanToReplaceByCompletionkAsync(documentText, documentCaret).ConfigureAwait(false);
            lastSpanToReplaceOnKeyDown = spanToReplace;

            switch (key.ObjectPattern)
            {
                case Home or (_, Home):
                case End or (_, End):
                case (Shift, LeftArrow or RightArrow or UpArrow or DownArrow or Home or End) or
                     (Control | Shift, LeftArrow or RightArrow or UpArrow or DownArrow or Home or End) or
                     (Control, A):
                    Close();
                    return;
                case LeftArrow or RightArrow:
                    int caretNew = documentCaret + (key.ConsoleKeyInfo.Key == LeftArrow ? -1 : 1);
                    if (caretNew < spanToReplace.Start || caretNew > spanToReplace.Start + spanToReplace.Length)
                    {
                        Close();
                        return;
                    }
                    break;
                case Escape:
                    Close();
                    key.Handled = true;
                    return;
                default:
                    break;
            }
        }
        else
        {
            if (completionListTriggered)
            {
                if (codePane.Selection is null)
                {
                    Open();
                }
                key.Handled = true;
            }
            return;
        }

        Debug.Assert(IsOpen);
        if (FilteredView.IsEmpty)
        {
            if (completionListTriggered)
            {
                Close();
                Open();
                key.Handled = true;
            }
            return;
        }

        //completion list is open and there are some items
        switch (key.ObjectPattern)
        {
            case DownArrow:
                this.FilteredView.IncrementSelectedIndex();
                key.Handled = true;
                break;
            case UpArrow:
                this.FilteredView.DecrementSelectedIndex();
                key.Handled = true;
                break;
            case var _ when configuration.KeyBindings.CommitCompletion.Matches(key.ConsoleKeyInfo):
                if (FilteredView.SelectedItem != null)
                {
                    await InsertCompletion(codePane.Document, FilteredView.SelectedItem).ConfigureAwait(false);
                    key.Handled = char.IsControl(key.ConsoleKeyInfo.KeyChar);
                }
                else
                {
                    Close();
                    break;
                }
                break;
            case var _ when configuration.KeyBindings.TriggerCompletionList.Matches(key.ConsoleKeyInfo):
                key.Handled = true;
                break;
            default:
                break;
        }
    }

    private bool EnoughRoomToDisplay(CodePane codePane) =>
        codePane.CodeAreaHeight - codePane.Cursor.Row >= VerticalPaddingHeight + configuration.MinCompletionItemsCount; // offset + top border + MinCompletionItemsCount + bottom border

    async Task IKeyPressHandler.OnKeyUp(KeyPress key)
    {
        if (!EnoughRoomToDisplay(codePane)) return;

        bool wasAlreadyOpen = IsOpen;

        if (!IsOpen)
        {
            if (!char.IsControl(key.ConsoleKeyInfo.KeyChar) &&
                await promptCallbacks.ShouldOpenCompletionWindowAsync(codePane.Document.GetText(), codePane.Document.Caret).ConfigureAwait(false))
            {
                Open();
            }
        }

        if (IsOpen)
        {
            var documentText = codePane.Document.GetText();
            int documentCaret = codePane.Document.Caret;
            var spanToReplace = await promptCallbacks.GetSpanToReplaceByCompletionkAsync(documentText, documentCaret).ConfigureAwait(false);

            if (wasAlreadyOpen &&
                lastSpanToReplaceOnKeyDown.TryGet(out var spanToReplaceOld))
            {
                if (spanToReplace.Start != spanToReplaceOld.Start && spanToReplace.End != spanToReplaceOld.End)
                {
                    Close();
                    return;
                }
            }

            if (allCompletions.Count == 0)
            {
                var completions = await promptCallbacks.GetCompletionItemsAsync(documentText, documentCaret, spanToReplace).ConfigureAwait(false);
                if (completions.Any())
                {
                    allCompletions = completions;
                    if (completions.Any())
                    {
                        int height = Math.Min(codePane.CodeAreaHeight - VerticalPaddingHeight, configuration.MaxCompletionItemsCount);
                        FilteredView.UpdateItems(completions, documentText, documentCaret, spanToReplace, height);
                    }
                }
                else
                {
                    Close();
                }
            }
            else if (!key.Handled)
            {
                FilteredView.Match(documentText, documentCaret, spanToReplace);
            }
        }
    }

    private async Task InsertCompletion(Document document, CompletionItem completion)
    {
        var spanToReplace = await promptCallbacks.GetSpanToReplaceByCompletionkAsync(document.GetText(), document.Caret).ConfigureAwait(false);
        document.Remove(spanToReplace);
        document.InsertAtCaret(completion.ReplacementText, codePane.GetSelectionSpan());
        document.Caret = spanToReplace.Start + completion.ReplacementText.Length;
        Close();
    }
}