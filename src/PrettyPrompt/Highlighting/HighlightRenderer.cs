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
            FormatSpan currentHighlight = null;

            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                WrappedLine line = lines[lineIndex];
                int lineFullWidthCharacterOffset = 0;
                var cells = Cell.FromText(line.Content);
                for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    var cell = cells[cellIndex];
                    if (cell.IsContinuationOfPreviousCharacter)
                        lineFullWidthCharacterOffset++;

                    // highlight wrapped lines
                    if (currentHighlight is not null && cellIndex == 0)
                    {
                        currentHighlight = HighlightSpan(currentHighlight, cells, cellIndex, currentHighlight.Start - line.StartIndex);
                    }

                    // get current highlight start
                    int characterPosition = line.StartIndex + cellIndex - lineFullWidthCharacterOffset;
                    currentHighlight ??= highlightsLookup.GetValueOrDefault(characterPosition);

                    // highlight based on start
                    if (currentHighlight is not null
                        && characterPosition >= currentHighlight.Start
                        && characterPosition < currentHighlight.Start + currentHighlight.Length)
                    {
                        currentHighlight = HighlightSpan(currentHighlight, cells, cellIndex, cellIndex);
                    }
                }
                highlightedRows[lineIndex] = new Row(cells);
            }
            return highlightedRows;
        }

        private static FormatSpan HighlightSpan(FormatSpan currentHighlight, List<Cell> cells, int cellIndex, int endPosition)
        {
            var highlightedFullWidthOffset = 0;
            int i;
            for (i = cellIndex; i < Math.Min(endPosition + currentHighlight.Length + highlightedFullWidthOffset, cells.Count); i++)
            {
                if (cells[i].ElementWidth == 2) highlightedFullWidthOffset++;
                cells[i].Formatting = currentHighlight?.Formatting;
            }
            if (i != cells.Count)
            {
                currentHighlight = null;
            }

            return currentHighlight;
        }
    }
}
