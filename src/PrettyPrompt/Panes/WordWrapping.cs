#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrettyPrompt.Consoles;
using PrettyPrompt.Rendering;

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
            int rowStartIndex = 0;
            foreach (ReadOnlyMemory<char> chunk in input.GetChunks())
            {
                foreach (char character in chunk.Span)
                {
                    line.Append(character);
                    bool isCursorPastCharacter = initialCaretPosition > textIndex;

                    int charWidth = UnicodeWidth.GetWidth(character);
                    currentLineLength += charWidth;
                    textIndex++;

                    if (isCursorPastCharacter && !char.IsControl(character))
                    {
                        cursor.Column += charWidth;
                    }
                    if (character == '\n' || currentLineLength == width)
                    {
                        if (isCursorPastCharacter)
                        {
                            cursor.Row++;
                            cursor.Column = 0;
                        }
                        lines.Add(new WrappedLine(rowStartIndex, line.ToString()));
                        line = new StringBuilder();
                        rowStartIndex += currentLineLength;
                        currentLineLength = 0;
                    }
                }
            }

            if (currentLineLength > 0)
                lines.Add(new WrappedLine(rowStartIndex, line.ToString()));

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
                int currentLineWidth = 0;
                foreach (var currentWord in line.Split(' ').SelectMany(word => word.SplitIntoSubstrings(maxLength)))
                {
                    var wordLength = UnicodeWidth.GetWidth(currentWord);
                    var wordWithSpaceLength = currentLineWidth == 0 ? wordLength : wordLength + 1;

                    if (currentLineWidth > maxLength
                        || currentLineWidth + wordWithSpaceLength > maxLength)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                        currentLineWidth = 0;
                    }

                    if(currentLineWidth == 0)
                    {
                        currentLine.Append(currentWord);
                        currentLineWidth += wordLength;
                    }
                    else
                    {
                        currentLine.Append(" " + currentWord);
                        currentLineWidth += wordLength + 1;
                    }
                }

                if (currentLineWidth > 0)
                {
                    lines.Add(currentLine.ToString());
                }
            }

            return lines;
        }
    }
}
