using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettyPrompt.Highlighting
{
    public delegate Task<IReadOnlyCollection<FormatSpan>> HighlightHandlerAsync(string text);

    static class SyntaxHighlighting
    {
        public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines)
        {
            var highlightsLookup = highlights.ToDictionary(h => h.Start);
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
