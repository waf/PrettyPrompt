#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Panes;

internal class CompletionPane : IKeyPressHandler
{
    /// <summary>
    /// Cursor + box borders.
    /// </summary>
    private const int VerticalPaddingHeight = 1 + BoxDrawing.VerticalBordersHeight;

    private readonly CodePane codePane;
    private readonly IPromptCallbacks promptCallbacks;
    private readonly PromptConfiguration configuration;
    private readonly OverloadPane overloadPane;

    private TextSpan? lastSpanToReplaceOnKeyDown;
    private bool completionListTriggeredOnKeyDown;

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

    public IReadOnlyList<FormattedString> SelectedItemDocumentation { get; private set; } = Array.Empty<FormattedString>();
    public int SelectedItemDocumentationWidth =>
        BoxDrawing.GetHorizontalBordersWidth(BoxType.TextLines, configuration) +
        (SelectedItemDocumentation.Count > 0 ? SelectedItemDocumentation.Max(l => l.GetUnicodeWidth()) : 0);

    public CompletionPane(
        CodePane codePane,
        OverloadPane overloadPane,
        IPromptCallbacks promptCallbacks,
        PromptConfiguration configuration)
    {
        this.codePane = codePane;
        this.promptCallbacks = promptCallbacks;
        this.configuration = configuration;
        this.overloadPane = overloadPane;

        FilteredView.SelectedItemChanged += SelectedItemChanged;
    }

    private void Open()
    {
        this.IsOpen = true;
        this.allCompletions = Array.Empty<CompletionItem>();
    }

    private async Task Close(CancellationToken cancellationToken)
    {
        IsOpen = false;
        await FilteredView.Clear(cancellationToken).ConfigureAwait(false);
    }

    async Task IKeyPressHandler.OnKeyDown(KeyPress key, CancellationToken cancellationToken)
    {
        var completionListTriggered = configuration.KeyBindings.TriggerCompletionList.Matches(key.ConsoleKeyInfo);
        completionListTriggeredOnKeyDown = completionListTriggered;
        lastSpanToReplaceOnKeyDown = null;

        if (IsOpen)
        {
            var documentText = codePane.Document.GetText();
            int documentCaret = codePane.Document.Caret;
            var spanToReplace = await promptCallbacks.GetSpanToReplaceByCompletionAsync(documentText, documentCaret, cancellationToken).ConfigureAwait(false);
            lastSpanToReplaceOnKeyDown = spanToReplace;

            switch (key.ObjectPattern)
            {
                case Home or (_, Home):
                case End or (_, End):
                case (Shift, LeftArrow or RightArrow or UpArrow or DownArrow or Home or End) or
                     (Control | Shift, LeftArrow or RightArrow or UpArrow or DownArrow or Home or End) or
                     (Control, A):
                    await Close(cancellationToken).ConfigureAwait(false);
                    return;
                case LeftArrow or RightArrow:
                    int caretNew = documentCaret + (key.ConsoleKeyInfo.Key == LeftArrow ? -1 : 1);
                    if (caretNew < spanToReplace.Start || caretNew > spanToReplace.Start + spanToReplace.Length)
                    {
                        await Close(cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    break;
                case Escape:
                    await Close(cancellationToken).ConfigureAwait(false);
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
                await Close(cancellationToken).ConfigureAwait(false);
                Open();
                key.Handled = true;
            }
            return;
        }

        //completion list is open and there are some items
        switch (key.ObjectPattern)
        {
            case DownArrow:
                await FilteredView.IncrementSelectedIndex(cancellationToken).ConfigureAwait(false);
                key.Handled = true;
                break;
            case UpArrow:
                await FilteredView.DecrementSelectedIndex(cancellationToken).ConfigureAwait(false);
                key.Handled = true;
                break;
            case var _ when configuration.KeyBindings.TriggerCompletionList.Matches(key.ConsoleKeyInfo):
                key.Handled = true;
                break;
            default:
                if (FilteredView.SelectedItem is null)
                {
                    if (configuration.KeyBindings.CommitCompletion.Matches(key.ConsoleKeyInfo))
                    {
                        await Close(cancellationToken).ConfigureAwait(false);
                    }
                }
                else if (
                    configuration.KeyBindings.CommitCompletion.Matches(key.ConsoleKeyInfo, FilteredView.SelectedItem.CommitCharacterRules) &&
                    await promptCallbacks.ConfirmCompletionCommit(codePane.Document.GetText(), codePane.Document.Caret, key, cancellationToken).ConfigureAwait(false))
                {
                    await InsertCompletion(codePane, FilteredView.SelectedItem, cancellationToken).ConfigureAwait(false);
                    key.Handled = char.IsControl(key.ConsoleKeyInfo.KeyChar);
                }
                break;
        }
    }

    async Task IKeyPressHandler.OnKeyUp(KeyPress key, CancellationToken cancellationToken)
    {
        bool wasAlreadyOpen = IsOpen;

        if (!IsOpen)
        {
            if (!char.IsControl(key.ConsoleKeyInfo.KeyChar) &&
                !completionListTriggeredOnKeyDown &&
                await promptCallbacks.ShouldOpenCompletionWindowAsync(codePane.Document.GetText(), codePane.Document.Caret, key, cancellationToken).ConfigureAwait(false))
            {
                Open();
            }
        }

        if (IsOpen)
        {
            var documentText = codePane.Document.GetText();
            int documentCaret = codePane.Document.Caret;
            var spanToReplace = await promptCallbacks.GetSpanToReplaceByCompletionAsync(documentText, documentCaret, cancellationToken).ConfigureAwait(false);

            if (wasAlreadyOpen &&
                lastSpanToReplaceOnKeyDown.TryGet(out var spanToReplaceOld))
            {
                if (spanToReplace.Start != spanToReplaceOld.Start && spanToReplace.End != spanToReplaceOld.End)
                {
                    await Close(cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            if (allCompletions.Count == 0)
            {
                var completions = await promptCallbacks.GetCompletionItemsAsync(documentText, documentCaret, spanToReplace, cancellationToken).ConfigureAwait(false);
                if (completions.Any())
                {
                    allCompletions = completions;
                    if (completions.Any())
                    {
                        int height = Math.Min(codePane.CodeAreaHeight - VerticalPaddingHeight - overloadPane.GetCurrentHeight(), configuration.MaxCompletionItemsCount);
                        await FilteredView.UpdateItems(completions, documentText, documentCaret, spanToReplace, height, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await Close(cancellationToken).ConfigureAwait(false);
                }
            }
            else if (!key.Handled)
            {
                await FilteredView.Match(documentText, documentCaret, spanToReplace, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task InsertCompletion(CodePane codepane, CompletionItem completion, CancellationToken cancellationToken)
    {
        var document = codepane.Document;
        var spanToReplace = await promptCallbacks.GetSpanToReplaceByCompletionAsync(document.GetText(), document.Caret, cancellationToken).ConfigureAwait(false);
        document.Remove(codepane, spanToReplace);
        document.InsertAtCaret(codepane, completion.ReplacementText);
        document.Caret = spanToReplace.Start + completion.ReplacementText.Length;
        await Close(cancellationToken).ConfigureAwait(false);
    }

    public bool WouldKeyPressCommitCompletionItem(KeyPress key) =>
        IsOpen &&
        FilteredView.SelectedItem != null &&
        configuration.KeyBindings.CommitCompletion.Matches(key.ConsoleKeyInfo, FilteredView.SelectedItem.CommitCharacterRules);

    private async Task SelectedItemChanged(CompletionItem? item, CancellationToken cancellationToken)
    {
        if (item is null || FilteredView.VisibleItemsCount == 0)
        {
            SelectedItemDocumentation = Array.Empty<FormattedString>();
            return;
        }

        var documentation = await item.GetExtendedDescriptionAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(documentation.Text))
        {
            SelectedItemDocumentation = Array.Empty<FormattedString>();
            return;
        }

        var completionPaneWidth = BoxDrawing.GetHorizontalBordersWidth(BoxType.CompletionItems, configuration) + FilteredView.VisibleItems.Max(i => i.DisplayTextFormatted.GetUnicodeWidth());
        var maxWidth = codePane.CodeAreaWidth - completionPaneWidth;
        if (maxWidth < 12)
        {
            SelectedItemDocumentation = Array.Empty<FormattedString>();
            return;
        }

        documentation = documentation.Replace("\r\n", "\n");
        var completionRowsCount = FilteredView.VisibleItemsCount + BoxDrawing.VerticalBordersHeight;

        // Request word wrapping. Actual line lengths won't be exactly the requested width due to wrapping.
        // We will try wrappings with different available horizontal sizes. We don't want
        // 'too long and too thin' boxes but also we don't want 'too narrow and too high' ones.
        // So we use two heuristics to select the 'right' proportions of the documentation box.
        List<FormattedString>? documentationLines = null;
        for (double proportion = 0.7; proportion <= 0.96; proportion += 0.05) //70%, 75%, ..., 95%
        {
            var requestedBoxWidth = (int)(proportion * maxWidth);
            documentationLines = GetDocumentationLines(requestedBoxWidth);

            var documentationBoxHeight = documentationLines.Count + BoxDrawing.VerticalBordersHeight;

            //Heuristic 1) primarily we want to use space preallocated by the completion items box.
            if (documentationBoxHeight <= completionRowsCount)
            {
                var documentationBoxWidth = GetActualTextWidth(documentationLines) + BoxDrawing.GetHorizontalBordersWidth(BoxType.TextLines, configuration);

                //Heuristic 2) we prefer boxes with an aspect ratio > 4 (which assumes we are trying different proportions in ascending order).
                const double MonospaceFontWidthHeightRatioApprox = 0.5;
                if (MonospaceFontWidthHeightRatioApprox * documentationBoxWidth / documentationBoxHeight > 4)
                {
                    break;
                }
            }
        }

        Debug.Assert(documentationLines != null);

        SelectedItemDocumentation = documentationLines;

        List<FormattedString> GetDocumentationLines(int requestedBoxWidth)
        {
            var requestedTextWidth = requestedBoxWidth - BoxDrawing.GetHorizontalBordersWidth(BoxType.TextLines, configuration);
            var documentationLines = WordWrapping.WrapWords(documentation, requestedTextWidth);
            return documentationLines;
        }

        static int GetActualTextWidth(List<FormattedString> documentationLines)
            => documentationLines.Max(line => line.GetUnicodeWidth());
    }
}