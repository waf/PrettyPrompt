#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Completion;

/// <summary>
/// A menu item in the Completion Menu Pane.
/// </summary>
[DebuggerDisplay("{DisplayText}")]
public class CompletionItem
{
    public delegate Task<FormattedString> GetExtendedDescriptionHandler(CancellationToken cancellationToken);

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
    /// Rules that modify the set of characters that can be typed to cause the selected item to be committed.
    /// </summary>
    public ImmutableArray<CharacterSetModificationRule> CommitCharacterRules { get; }

    /// <summary>
    /// This task will be executed when the item is selected, to display the extended "tool tip" description to the right of the menu.
    /// </summary>
    public Task<FormattedString> GetExtendedDescriptionAsync(CancellationToken cancellationToken) => getExtendedDescription(cancellationToken);

    private readonly GetExtendedDescriptionHandler getExtendedDescription;

    /// <param name="replacementText">When the completion item is selected, this text will be inserted into the document at the specified start index.</param>
    /// <param name="displayText">This text will be displayed in the completion menu. If not specified, the <paramref name="replacementText"/> value will be used.</param>
    /// <param name="getExtendedDescription">This lazy task will be executed when the item is selected, to display the extended "tool tip" description to the right of the menu.</param>
    /// <param name="filterText">The text used to determine if the item matches the filter in the list. If not specified the <paramref name="replacementText"/> value is used.</param>
    /// <param name="commitCharacterRules">Rules that modify the set of characters that can be typed to cause the selected item to be committed.</param>
    public CompletionItem(
        string replacementText,
        FormattedString displayText = default,
        string? filterText = null,
        GetExtendedDescriptionHandler? getExtendedDescription = null,
        ImmutableArray<CharacterSetModificationRule> commitCharacterRules = default)
    {
        ReplacementText = replacementText;
        DisplayTextFormatted = displayText.IsEmpty ? replacementText : displayText;
        FilterText = filterText ?? replacementText;
        CommitCharacterRules = commitCharacterRules.IsDefault ? ImmutableArray<CharacterSetModificationRule>.Empty : commitCharacterRules;

        Task<FormattedString>? extendedDescriptionTask = null; //will be stored in closure of getExtendedDescription
        this.getExtendedDescription = ct => extendedDescriptionTask ??= getExtendedDescription?.Invoke(ct) ?? Task.FromResult(FormattedString.Empty);
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

        int priority;
        if (valueLonger.StartsWith(valueShorter, StringComparison.CurrentCulture))
        {
            priority = 4;
        }
        else if (valueLonger.StartsWith(valueShorter, StringComparison.CurrentCultureIgnoreCase))
        {
            priority = 3;
        }
        else if (valueLonger.Contains(valueShorter, StringComparison.CurrentCulture))
        {
            priority = 2;
        }
        else if (valueLonger.Contains(valueShorter, StringComparison.CurrentCultureIgnoreCase))
        {
            priority = 1;
        }
        else
        {
            return int.MinValue; //completely non-matching item
        }

        if (pattern.Length <= FilterText.Length)
        {
            //matching item
            return priority;
        }
        else
        {
            //non-matching item, but it's contained in pattern (which is better than completely unmatching)
            return int.MinValue + priority;
        }
    }
}