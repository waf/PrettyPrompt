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
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="spanToBeReplaced">Span of text that will be replaced by inserted completion item</param>
    /// <returns>
    /// Integer representing priority of the item in completion list.
    /// </returns>
    public virtual int GetCompletionItemPriority(string text, int caret, TextSpan spanToBeReplaced)
    {
        if (spanToBeReplaced.IsEmpty)
        {
            return 0;
        }

        var pattern = text.AsSpan(spanToBeReplaced);
        var patternCropped = pattern[..Math.Min(pattern.Length, FilterText.Length)];
        if (FilterText.AsSpan().StartsWith(patternCropped, StringComparison.CurrentCultureIgnoreCase))
        {
            return Math.Abs(pattern.Length - FilterText.Length);
        }

        if (FilterText.AsSpan().Contains(patternCropped, StringComparison.CurrentCultureIgnoreCase))
        {
            return 1000 + Math.Abs(pattern.Length - FilterText.Length);
        }

        return int.MaxValue;
    }
}