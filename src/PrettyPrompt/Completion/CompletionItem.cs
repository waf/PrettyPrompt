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
    public virtual double GetCompletionItemPriority(string text, int caret, TextSpan spanToBeReplaced)
    {
        if (spanToBeReplaced.IsEmpty)
        {
            return 0;
        }

        const double MaxPriority = 10_000_000f;
        const double PriorityChunk = 1_000_000f;
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

        double distance;
        if (valueLonger.StartsWith(valueShorter, StringComparison.CurrentCultureIgnoreCase))
        {
            distance = LevenshteinDistance(valueShorter, valueLonger);
        }
        else if (valueLonger.Contains(valueShorter, StringComparison.CurrentCultureIgnoreCase))
        {
            distance = PriorityChunk + LevenshteinDistance(valueShorter, valueLonger);
        }
        else
        {
            return double.MinValue; //completely non-matching item
        }

        if (pattern.Length <= FilterText.Length)
        {
            //matching item
            return MaxPriority - distance;
        }
        else
        {
            //non-matching item, but it's contained in pattern (which is better than completely unmatching)
            return -distance;
        }
    }

    internal static double LevenshteinDistance(ReadOnlySpan<char> s, ReadOnlySpan<char> t, double letterCaseChange= 0.001)
    {
        //https://en.wikipedia.org/wiki/Levenshtein_distance

        // create two work vectors of integer distances
        Span<double> v0 = stackalloc double[t.Length + 1];
        Span<double> v1 = stackalloc double[t.Length + 1];

        // initialize v0 (the previous row of distances)
        // this row is A[0][i]: edit distance from an empty s to t;
        // that distance is the number of characters to append to s to make t.
        for (int i = 0; i <= t.Length; i++)
        {
            v0[i] = i;
        }

        for (int i = 0; i < s.Length; i++)
        {
            // calculate v1 (current row distances) from the previous row v0

            // first element of v1 is A[i + 1][0]
            //   edit distance is delete (i + 1) chars from s to match empty t
            v1[0] = i + 1;

            // use formula to fill in the rest of the row
            for (int j = 0; j < t.Length; j++)
            {
                // calculating costs for A[i + 1][j + 1]
                var deletionCost = v0[j + 1] + 1;
                var insertionCost = v1[j] + 1;

                double substitutionCost = v0[j];
                if (s[i] != t[j])
                {
                    substitutionCost +=
                        char.ToUpperInvariant(s[i]) == char.ToUpperInvariant(t[j]) ?
                        letterCaseChange :
                        1d;
                }

                v1[j + 1] = Math.Min(Math.Min(deletionCost, insertionCost), substitutionCost);
            }

            // copy v1 (current row) to v0 (previous row) for next iteration
            // since data in v1 is always invalidated, a swap without copy could be more efficient
            var vTmp = v0;
            v0 = v1;
            v1 = vTmp;
        }
        // after the last swap, the results of v1 are now in v0
        return v0[t.Length];
    }
}