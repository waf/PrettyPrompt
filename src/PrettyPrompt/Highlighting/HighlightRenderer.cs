#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrettyPrompt.Highlighting
{
    static class HighlightRenderer
    {
        public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines)
        {
            var highlightsLookup = highlights
                .ToLookup(h => h.Start)
                .ToDictionary(h => h.Key, conflictingHighlights => conflictingHighlights.OrderByDescending(h => h.Length).First());
            Row[] highlightedRows = new Row[lines.Count];
            FormatSpan wrappingHighlight = null;

            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                WrappedLine line = lines[lineIndex];
                var cells = Cell.FromText(line.Content).ToArray();
                for (int charIndex = 0; charIndex < cells.Length; charIndex++)
                {
                    if (highlightsLookup.TryGetValue(line.StartIndex + charIndex, out FormatSpan highlight))
                    {
                        charIndex = ApplyHighlight(highlight, line.StartIndex, charIndex, cells);
                        // we've hit the end of the line, we need to continue the highlight on the next row.
                        if (charIndex == cells.Length)
                        {
                            wrappingHighlight = highlight;
                        }
                        charIndex--; // outer loop will increment, skipping a string index to check for highlighting.
                    }
                    else if (wrappingHighlight is not null && ShouldApplyWrappedHighlighting(wrappingHighlight, line, charIndex))
                    {
                        cells[charIndex].Formatting = wrappingHighlight.Formatting;
                    }
                }
                highlightedRows[lineIndex] = new Row(cells);
            }
            return highlightedRows;
        }

        private static int ApplyHighlight(FormatSpan highlight, int lineStartIndex, int charNumber, Cell[] cells)
        {
            var highlightEnd = Math.Min(highlight.Start + highlight.Length - lineStartIndex, cells.Length);
            for (; charNumber < highlightEnd; charNumber++)
            {
                cells[charNumber].Formatting = highlight.Formatting;
            }
            return charNumber;
        }

        private static bool ShouldApplyWrappedHighlighting(FormatSpan wrappingHighlight, WrappedLine line, int charNumber) =>
            line.StartIndex + charNumber < wrappingHighlight.Start + wrappingHighlight.Length;
    }
}
