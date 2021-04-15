using PrettyPrompt.Panes;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettyPrompt.Highlighting
{
    public delegate Task<IReadOnlyCollection<FormatSpan>> HighlightHandlerAsync(string text);

    static class SyntaxHighlighting
    {
        public static Cell[] ApplyColorToCharacters(Dictionary<int, FormatSpan> highlightsLookup, WrappedLine line)
        {
            var text = Cell.FromText(line.Content).ToArray();
            for (int i = 0; i < text.Length; i++)
            {
                if (highlightsLookup.TryGetValue(line.StartIndex + i, out var highlight))
                {
                    for(; i < highlight.Start + highlight.Length - line.StartIndex; i++)
                    {
                        text[i].Formatting = highlight.Formatting;
                    }
                    i--; // outer loop will increment, skipping a string index to check for highlighting.
                }
            }
            return text;
        }
    }
}
