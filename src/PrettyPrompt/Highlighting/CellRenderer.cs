#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Diagnostics;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.TextSelection;

namespace PrettyPrompt.Highlighting;

/// <summary>
/// Given the text and the syntax highlighting information, render the text into the "cells" of the terminal screen.
/// </summary>
internal static class CellRenderer
{
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, SelectionSpan? selection, AnsiColor? selectedTextBackground)
        => ApplyColorToCharacters(highlights, lines, selection, selectedTextBackground, startLine: 0, endLine: lines.Count);

    /// <summary>
    /// Builds the <see cref="Row"/>/<see cref="Cell"/>s for the wrapped lines in the half-open range
    /// [<paramref name="startLine"/>, <paramref name="endLine"/>). Building only the visible range (instead
    /// of the whole document and then discarding off-screen rows) keeps per-keystroke cost and allocation
    /// bounded by the viewport rather than the document size (see PERFORMANCE_PLAN.md Tier C).
    ///
    /// When <paramref name="startLine"/> &gt; 0, the two pieces of state a full top-down pass would have
    /// carried across the skipped lines are seeded explicitly:
    ///   - <c>currentHighlight</c>: a multi-line highlight span that began above the viewport and is still open.
    ///   - <c>selectionHighlight</c>: whether the text selection is already "open" at the top of the viewport.
    /// </summary>
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, SelectionSpan? selection, AnsiColor? selectedTextBackground, int startLine, int endLine)
    {
        Debug.Assert(startLine >= 0 && startLine <= endLine && endLine <= lines.Count);

        var selectionStart = new ConsoleCoordinate(int.MaxValue, int.MaxValue); //invalid
        var selectionEnd = new ConsoleCoordinate(int.MaxValue, int.MaxValue); //invalid
        if (selection.TryGet(out var selectionValue))
        {
            selectionStart = selectionValue.Start;
            selectionEnd = selectionValue.End;
        }

        // If the selection began above the viewport and hasn't ended yet, it's already "open" at startLine.
        bool selectionHighlight = selectionStart.Row < startLine && selectionEnd.Row >= startLine;

        // Only spans STARTING in the viewport can be found by the per-cell lookup below; one that began above
        // it is handled by SeedCurrentHighlight. Building the lookup from every span in the document was
        // O(document) per render - ~36% of a caret-move keystroke at 1000 lines / ~14.7k spans.
        var (viewPortStartChar, viewPortEndChar) = GetViewPortCharRange(lines, startLine, endLine);
        var highlightsLookup = HighlightsGroupingPool.Shared.Get(highlights, viewPortStartChar, viewPortEndChar);
        var highlightedRows = new Row[endLine - startLine];
        FormatSpan? currentHighlight = SeedCurrentHighlight(highlights, lines, startLine);
        for (int lineIndex = startLine; lineIndex < endLine; lineIndex++)
        {
            WrappedLine line = lines[lineIndex];
            // characterPosition coordinates are UTF-16 document offsets (the same units the highlight spans and
            // selection columns use). A cell, by contrast, is a display column. These two differ whenever a
            // grapheme cluster is not exactly one char wide AND one column wide:
            //   CJK '界'      -> 1 char,  2 columns
            //   emoji '🙃'     -> 2 chars (surrogate pair), 2 columns
            //   combining 'é' -> 2 chars, 1 column
            // so we track the cluster's UTF-16 length (lineCharOffset) independently of the cell index.
            int lineCharOffset = 0;           // UTF-16 offset within the line of the current cell's cluster
            int currentClusterCharLength = 0; // UTF-16 length of that cluster, captured from its main cell
            var row = new Row(line.Content);
            for (int cellIndex = 0; cellIndex < row.Length; cellIndex++)
            {
                var cell = row[cellIndex];
                // A main (non-continuation) cell starts a new cluster: advance past the previous cluster's chars.
                // Continuation cells belong to the same cluster as their main cell and share its char offset.
                if (!cell.IsContinuationOfPreviousCharacter)
                {
                    lineCharOffset += currentClusterCharLength;
                    currentClusterCharLength = cell.Text?.Length ?? 1;
                }
                int characterPosition = line.StartIndex + lineCharOffset;

                // syntax highlight wrapped lines (a highlight carried over from the previous line)
                if (currentHighlight.TryGet(out var previousLineHighlight) &&
                    cellIndex == 0)
                {
                    currentHighlight = HighlightSpan(previousLineHighlight, row, cellIndex, characterPosition);
                }

                // get current syntaxt highlight start
                currentHighlight ??= highlightsLookup.TryGetValue(characterPosition, out var lookupHighlight) ? lookupHighlight : null;

                // syntax highlight based on start
                if (currentHighlight.TryGet(out var highlight) &&
                    highlight.Contains(characterPosition))
                {
                    currentHighlight = HighlightSpan(highlight, row, cellIndex, characterPosition);
                }

                // if there's text selected, invert colors to represent the highlight of the selected text.
                if (selectionStart.Equals(lineIndex, lineCharOffset)) //start is inclusive
                {
                    selectionHighlight = true;
                }
                if (selectionEnd.Equals(lineIndex, lineCharOffset)) //end is exclusive
                {
                    selectionHighlight = false;
                }
                if (selectionHighlight)
                {
                    if (selectedTextBackground.TryGet(out var background))
                    {
                        cell.TransformBackground(background);
                    }
                    else
                    {
                        cell.Formatting = new ConsoleFormat { Inverted = true };
                    }
                }
            }
            highlightedRows[lineIndex - startLine] = row;
        }

        // Return the lookup to the pool. The dict is local and its values (FormatSpan/ConsoleFormat) are value
        // types copied into the cells, so nothing outlives this call. Without this Put the pool stayed empty and
        // every render allocated a fresh dictionary sized to ALL highlight spans (large in highlight-heavy docs).
        HighlightsGroupingPool.Shared.Put(highlightsLookup);
        return highlightedRows;
    }

    /// <summary>
    /// Half-open range of UTF-16 document offsets covered by lines [<paramref name="startLine"/>,
    /// <paramref name="endLine"/>) - every <c>characterPosition</c> the highlight lookup can be asked about.
    /// </summary>
    private static (int Start, int End) GetViewPortCharRange(IReadOnlyList<WrappedLine> lines, int startLine, int endLine)
    {
        if (startLine >= endLine)
        {
            return (0, 0);
        }
        var firstLine = lines[startLine];
        var lastLine = lines[endLine - 1];
        return (firstLine.StartIndex, lastLine.StartIndex + lastLine.Content.Length);
    }

    /// <summary>
    /// When rendering starts partway down the document (<paramref name="startLine"/> &gt; 0), find the
    /// highlight span the top-down pass would have been carrying into <paramref name="startLine"/>: one that
    /// began strictly before this line's first character and still covers it. Returns null when starting at
    /// the top, or when no span straddles the viewport's top boundary.
    /// </summary>
    private static FormatSpan? SeedCurrentHighlight(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, int startLine)
    {
        if (startLine == 0)
        {
            return null;
        }

        int startCharIndex = lines[startLine].StartIndex;
        FormatSpan? seed = null;
        foreach (var span in highlights)
        {
            if (span.Start < startCharIndex && span.Contains(startCharIndex))
            {
                // Prefer the span that began closest to the boundary (and, on a tie, the longest) so we
                // match the single span the top-down carry would be holding. For the usual disjoint
                // (non-overlapping) syntax-highlight spans there is at most one candidate.
                if (seed is null
                    || span.Start > seed.Value.Start
                    || (span.Start == seed.Value.Start && span.Length > seed.Value.Length))
                {
                    seed = span;
                }
            }
        }
        return seed;
    }

    /// <summary>
    /// Colors the cells of <paramref name="row"/> from <paramref name="cellIndex"/> forward that fall within
    /// <paramref name="currentHighlight"/>, stopping once the running UTF-16 document offset reaches the
    /// highlight's end (or the row ends). <paramref name="charOffset"/> is the UTF-16 offset of the cluster at
    /// <paramref name="cellIndex"/>.
    /// </summary>
    private static FormatSpan? HighlightSpan(FormatSpan currentHighlight, Row row, int cellIndex, int charOffset)
    {
        int i;
        for (i = cellIndex; i < row.Length && charOffset < currentHighlight.End; i++)
        {
            row[i].Formatting = currentHighlight.Formatting;
            if (!row[i].IsContinuationOfPreviousCharacter)
            {
                charOffset += row[i].Text?.Length ?? 1;
            }
        }
        if (i != row.Length)
        {
            return null;
        }

        return currentHighlight;
    }

    /// <summary>
    /// This is just an extra function used by <see cref="Prompt.RenderAnsiOutput"/> that highlights arbitrary text. It's
    /// not used for drawing input during normal functioning of the prompt.
    /// </summary>
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, string text, int textWidth)
    {
        var wrapped = WordWrapping.WrapEditableCharacters(new StringBuilder(text), 0, textWidth);
        return ApplyColorToCharacters(highlights, wrapped.WrappedLines, selection: null, selectedTextBackground: null);
    }

    private sealed class HighlightsGroupingPool : LockFreePool<Dictionary<int, FormatSpan>>
    {
        public static readonly HighlightsGroupingPool Shared = new();

        // One lookup is in flight per render (occasionally two when panes render), so a small cap is plenty.
        private HighlightsGroupingPool() : base(maxRetained: 8) { }

        /// <summary>
        /// Builds the start-offset -&gt; span lookup from only the spans starting within
        /// [<paramref name="viewPortStartChar"/>, <paramref name="viewPortEndChar"/>) - the only ones the
        /// caller can look up. The rest cost two int comparisons instead of a hash and a probe.
        /// </summary>
        public Dictionary<int, FormatSpan> Get(IReadOnlyCollection<FormatSpan> highlights, int viewPortStartChar, int viewPortEndChar)
        {
            var result = Rent() ?? new Dictionary<int, FormatSpan>();

            foreach (var highlight in highlights)
            {
                if (highlight.Start < viewPortStartChar || highlight.Start >= viewPortEndChar)
                {
                    continue;
                }

                if (result.TryGetValue(highlight.Start, out var formatSpan))
                {
                    if (highlight.Length > formatSpan.Length)
                    {
                        result[highlight.Start] = highlight;
                    }
                }
                else
                {
                    result.Add(highlight.Start, highlight);
                }
            }

            return result;
        }

        public void Put(Dictionary<int, FormatSpan> lookup)
        {
            lookup.Clear();
            ReturnToPool(lookup);
        }
    }
}