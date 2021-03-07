using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static PrettyPrompt.AnsiEscapeCodes;

namespace PrettyPrompt.Highlighting
{
    class SyntaxHighlighting
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
                if(lineStart < formattingEnd && formattingEnd <= lineEnd)
                {
                    text.Insert(formattingEnd - lineStart, ResetFormatting);
                }
                if(lineStart <= formattingStart && formattingStart <= lineEnd)
                {
                    text.Insert(formattingStart - lineStart, ToAnsiEscapeSequence(formatting.Formatting));
                }
            }
            return text.ToString();
        }
    }
}
