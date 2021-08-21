#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Documents;
using PrettyPrompt.TextSelection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrettyPrompt.Highlighting
{
    /// <summary>
    /// Given the text and the syntax highlighting information, render the text into the "cells" of the terminal screen.
    /// </summary>
    static class CellRenderer
    {
        public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, IList<SelectionSpan> selections)
        {
            var selectionStart = selections.Select(s => (s.Start.Row, s.Start.Column)).ToHashSet();
            var selectionEnd = selections.Select(s => (s.End.Row, s.End.Column)).ToHashSet();
            bool selectionHighlight = false;

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

                    // syntax highlight wrapped lines
                    if (currentHighlight is not null && cellIndex == 0)
                    {
                        currentHighlight = HighlightSpan(currentHighlight, cells, cellIndex, currentHighlight.Start - line.StartIndex);
                    }

                    // get current syntaxt highlight start
                    int characterPosition = line.StartIndex + cellIndex - lineFullWidthCharacterOffset;
                    currentHighlight ??= highlightsLookup.GetValueOrDefault(characterPosition);

                    // syntax highlight based on start
                    if (currentHighlight is not null
                        && characterPosition >= currentHighlight.Start
                        && characterPosition < currentHighlight.Start + currentHighlight.Length)
                    {
                        currentHighlight = HighlightSpan(currentHighlight, cells, cellIndex, cellIndex);
                    }

                    // if there's text selected, invert colors to represent the highlight of the selected text.
                    if (selectionStart.Contains((lineIndex, cellIndex - lineFullWidthCharacterOffset)))
                    {
                        selectionHighlight = true;
                    }
                    if(selectionHighlight)
                    {
                        cell.Formatting = new ConsoleFormat { Inverted = true };
                    }
                    if (selectionEnd.Contains((lineIndex, cellIndex - lineFullWidthCharacterOffset)))
                    {
                        selectionHighlight = false;
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
                highlightedFullWidthOffset += cells[i].ElementWidth - 1;
                cells[i].Formatting = currentHighlight?.Formatting;
            }
            if (i != cells.Count)
            {
                currentHighlight = null;
            }

            return currentHighlight;
        }

        /// <summary>
        /// This is just an extra function used by <see cref="Prompt.RenderAnsiOutput"/> that highlights arbitrary text. It's
        /// not used for drawing input during normal functioning of the prompt.
        /// </summary>
        public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, string text, int textWidth)
        {
            var wrapped = WordWrapping.WrapEditableCharacters(new System.Text.StringBuilder(text), 0, textWidth);
            return ApplyColorToCharacters(highlights, wrapped.WrappedLines, Array.Empty<SelectionSpan>());
        }
    }
}
