using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;

namespace PrettyPrompt.Highlighting
{
    public delegate Task<IReadOnlyCollection<FormatSpan>> HighlightHandlerAsync(string text);

    static class SyntaxHighlighting
    {
        public static string ApplyHighlighting(IReadOnlyCollection<FormatSpan> highlights, WrappedLine line)
        {
            var text = new StringBuilder(line.Content);
            foreach (var formatting in highlights.Reverse())
            {
                var lineStart = line.StartIndex;
                var lineEnd = line.StartIndex + text.Length;
                var formattingStart = formatting.Start;
                var formattingEnd = formatting.Start + formatting.Length;
                if (lineStart < formattingEnd && formattingEnd <= lineEnd)
                {
                    text.Insert(formattingEnd - lineStart, ResetFormatting);
                }
                if (lineStart <= formattingStart && formattingStart <= lineEnd)
                {
                    text.Insert(formattingStart - lineStart, ToAnsiEscapeSequence(formatting.Formatting));
                }
            }
            return text.ToString();
        }
    }
}
