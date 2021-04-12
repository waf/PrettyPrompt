using System;
using System.Collections.Generic;
using System.Text;
using PrettyPrompt.Consoles;

namespace PrettyPrompt.Panes
{
    record WordWrappedText(IReadOnlyList<WrappedLine> WrappedLines, ConsoleCoordinate Cursor);

    static class WordWrapping
    {
        public static WordWrappedText Wrap(StringBuilder input, int initialCaretPosition, int width)
        {
            var cursor = new ConsoleCoordinate(0, 0);
            if (input.Length == 0)
            {
                cursor.Column = initialCaretPosition;
                return new WordWrappedText(new[] { new WrappedLine(0, string.Empty) }, cursor);
            }

            var lines = new List<WrappedLine>();
            int currentLineLength = 0;
            var line = new StringBuilder(width);
            int textIndex = 0;
            foreach (ReadOnlyMemory<char> chunk in input.GetChunks())
            {
                foreach (char character in chunk.Span)
                {
                    line.Append(character);
                    bool isCursorPastCharacter = initialCaretPosition > textIndex;

                    currentLineLength++;
                    textIndex++;

                    if (isCursorPastCharacter && !char.IsControl(character))
                    {
                        cursor.Column++;
                    }
                    if (character == '\n' || currentLineLength == width)
                    {
                        if (isCursorPastCharacter)
                        {
                            cursor.Row++;
                            cursor.Column = 0;
                        }
                        lines.Add(new WrappedLine(textIndex - currentLineLength, line.ToString()));
                        line = new StringBuilder();
                        currentLineLength = 0;
                    }
                }
            }

            if (currentLineLength > 0)
                lines.Add(new WrappedLine(textIndex - currentLineLength, line.ToString()));

            return new WordWrappedText(lines, cursor);
        }
    }
}
