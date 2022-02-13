#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Completion;

/// <summary>
/// A menu item in the Completion Menu Pane.
/// </summary>
[DebuggerDisplay("{DisplayText}")]
public class CompletionItem
{
    /// <summary>
    /// When the completion item is selected, this text will be inserted into the document at the specified start index.
    /// </summary>
    public string ReplacementText { get; }

    /// <summary>
    /// This text will be displayed in the completion menu.
    /// </summary>
    public FormattedString DisplayTextFormatted { get; }

    /// <summary>
    /// This text will be displayed in the completion menu.
    /// </summary>
    public string DisplayText => DisplayTextFormatted.Text!;

    /// <summary>
    /// The text used to determine if the item matches the filter in the list.
    /// </summary>
    public string FilterText { get; }

    /// <summary>
    /// This task will be executed when the item is selected, to display the extended "tool tip" description to the right of the menu.
    /// </summary>
    public Task<FormattedString> GetExtendedDescriptionAsync()
        => extendedDescription?.Value ?? Task.FromResult(FormattedString.Empty);

    private readonly Lazy<Task<FormattedString>>? extendedDescription;

    /// <param name="replacementText">When the completion item is selected, this text will be inserted into the document at the specified start index.</param>
    /// <param name="displayText">This text will be displayed in the completion menu. If not specified, the <paramref name="replacementText"/> value will be used.</param>
    /// <param name="extendedDescription">This lazy task will be executed when the item is selected, to display the extended "tool tip" description to the right of the menu.</param>
    /// <param name="filterText">The text used to determine if the item matches the filter in the list. If not specified the <paramref name="replacementText"/> value is used.</param>
    public CompletionItem(
        string replacementText,
        FormattedString displayText = default,
        string? filterText = null,
        Lazy<Task<FormattedString>>? extendedDescription = null)
    {
        ReplacementText = replacementText;
        DisplayTextFormatted = displayText.IsEmpty ? replacementText : displayText;
        FilterText = filterText ?? replacementText;
        this.extendedDescription = extendedDescription;
    }

    /// <summary>
    /// Determines the completion item priority in completion list with respect to currently written text.
    /// Higher numbers represent higher priority. Highest priority item will be on top of the completion list.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="spanToBeReplaced">Span of text that will be replaced by inserted completion item</param>
    /// <returns>
    /// Integer representing priority of the item in completion list. Negative priorities represents
    /// non-matching items.
    /// </returns>
    public virtual int GetCompletionItemPriority(string text, int caret, TextSpan spanToBeReplaced)
    {
        if (spanToBeReplaced.IsEmpty)
        {
            return 0;
        }

        const int PriorityChunk = 1000;
        var pattern = text.AsSpan(spanToBeReplaced);

        ReadOnlySpan<char> valueLonger;
        ReadOnlySpan<char> valueShorter;
        if (pattern.Length <= FilterText.Length)
        {
            valueLonger = FilterText;
            valueShorter = pattern;
        }
        else
        {
            valueLonger = pattern;
            valueShorter = FilterText;
        }

        int distance;
        if (valueLonger.StartsWith(valueShorter, StringComparison.CurrentCultureIgnoreCase))
        {
            distance = Math.Abs(valueShorter.Length - valueLonger.Length);
        }
        else if (valueLonger.Contains(valueShorter, StringComparison.CurrentCultureIgnoreCase))
        {
            distance = PriorityChunk + Math.Abs(valueShorter.Length - valueLonger.Length);
        }
        else
        {
            return int.MinValue; //completely non-matching item
        }

        if (pattern.Length <= FilterText.Length)
        {
            //matching item
            return int.MaxValue - distance;
        }
        else
        {
            //non-matching item, but it's contained in pattern (which is better than completely unmatching)
            return -distance;
        }
    }
}