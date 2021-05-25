using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrettyPrompt.Consoles;

namespace PrettyPrompt.Panes
{
    record WordWrappedText(IReadOnlyList<WrappedLine> WrappedLines, ConsoleCoordinate Cursor);

    static class WordWrapping
    {
        /// <summary>
        /// Wraps editable input, contained in the string builder, to the supplied width.
        /// The caret index (as the input is a 1 dimensional string of text) is converted
        /// to a 2 dimensional coordinate in the wrapped text.
        /// </summary>
        public static WordWrappedText WrapEditableCharacters(StringBuilder input, int initialCaretPosition, int width)
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


        /// <summary>
        /// Wrap words into lines of at most maxLength long. Split on spaces
        /// where possible, otherwise split by character if a single word is
        /// greater than maxLength.
        /// </summary>
        public static List<string> WrapWords(string text, int maxLength)
        {
            if (text.Length == 0)
            {
                return new List<string>();
            }

            var lines = new List<string>();
            foreach (var line in text.Split('\n'))
            {
                var currentLine = new StringBuilder();
                foreach (var currentWord in line.Split(' ').SelectMany(word => word.SplitIntoSubstrings(maxLength)))
                {
                    var wordWithSpace = currentLine.Length == 0 ? currentWord : " " + currentWord;

                    if (currentLine.Length > maxLength
                        || currentLine.Length + wordWithSpace.Length > maxLength)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }

                    currentLine.Append(currentLine.Length == 0 ? currentWord : " " + currentWord);
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                }
            }

            return lines;
        }
    }
}
