#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;

namespace PrettyPrompt.Documents;

internal static class WordWrapping
{
    /// <summary>
    /// Wraps editable input, contained in the string builder, to the supplied width.
    /// The caret index (as the input is a 1 dimensional string of text) is converted
    /// to a 2 dimensional coordinate in the wrapped text.
    /// </summary>
    public static WordWrappedText WrapEditableCharacters(ReadOnlyStringBuilder input, int caret, int width)
    {
        Debug.Assert(caret >= 0 && caret <= input.Length);

        if (input.Length == 0)
        {
            return new WordWrappedText(
                new[] { WrappedLine.Empty(startIndex: 0) },
                new ConsoleCoordinate(0, caret));
        }

        var lines = new List<WrappedLine>();
        int currentLineLength = 0;
        var line = StringBuilderPool.Shared.Get(width);
        int textIndex = 0;
        int charsDroppedFromLine = 0;
        int cursorColumn = 0;
        int cursorRow = 0;
        foreach (var chunkMemory in input.GetChunks())
        {
            var chunk = chunkMemory.Span;
            for (var i = 0; i < chunk.Length; i++)
            {
                char character = chunk[i];

                // treat "\r\n" as a single line break: drop the '\r' from the wrapped content (a literal
                // carriage return rendered to the terminal desyncs the renderer's cursor tracking), but still
                // advance textIndex so WrappedLine.StartIndex keeps matching offsets into the original text
                // (callers' highlight spans are keyed to them). Editable input is normalized to '\n' before it
                // reaches the document; this matters for Prompt.RenderAnsiOutput, which accepts arbitrary text.
                if (character == '\r' &&
                    textIndex + 1 < input.Length &&
                    (i + 1 < chunk.Length ? chunk[i + 1] : input[textIndex + 1]) == '\n')
                {
                    textIndex++;
                    charsDroppedFromLine++;
                    continue;
                }

                line.Append(character);
                bool isCursorPastCharacter = caret > textIndex;

                Debug.Assert(character != '\t', "tabs should be replaced by spaces");
                // Zero-width scalars (combining marks, zero-width joiners, etc.) are legitimate input - e.g.
                // when pasting an emoji such as 🤦🏼‍♂️. They contribute no display width (so they don't grow the
                // line or trigger wrapping), but they ARE part of the string/line content, so they must still
                // advance textIndex (the string index) and the caret column just like any other character.
                // (The emoji variation selector U+FE0F is the exception: GetWidth gives it one column, because
                // it promotes its base to a 2-column emoji - see UnicodeWidth.) See issue #270.
                int unicodeWidth = UnicodeWidth.GetWidth(character);
                currentLineLength += unicodeWidth;
                textIndex++;

                if (isCursorPastCharacter && !char.IsControl(character))
                {
                    cursorColumn++;
                }
                if (character == '\n' || currentLineLength == width ||
                    NextCharacterIsFullWidthAndWillWrap(width, currentLineLength, chunk, i))
                {
                    if (isCursorPastCharacter)
                    {
                        cursorRow++;
                        cursorColumn = 0;
                    }
                    lines.Add(new WrappedLine(textIndex - line.Length - charsDroppedFromLine, line.ToString()));
                    line.Clear();
                    currentLineLength = 0;
                    charsDroppedFromLine = 0;
                }
            }
        }

        // Flush on `line.Length`, not `currentLineLength`: a trailing line consisting only of zero-width
        // characters (e.g. a lone combining mark such as the Thai tone mark U+0E49) has zero display width but
        // real content that must still be emitted. Testing display width here dropped that content and left the
        // caret column pointing past the (empty) trailing line, tripping the cursor assertion. See issue #270.
        if (line.Length > 0 || input[^1] == '\n')
        {
            lines.Add(new WrappedLine(textIndex - line.Length - charsDroppedFromLine, line.ToString()));
        }

        Debug.Assert(textIndex == input.Length);
        if (cursorRow >= lines.Count)
        {
            Debug.Assert(cursorRow == lines.Count);
            lines.Add(WrappedLine.Empty(startIndex: textIndex));
        }

        StringBuilderPool.Shared.Put(line);
        return new WordWrappedText(lines, new ConsoleCoordinate(cursorRow, cursorColumn));

        static bool NextCharacterIsFullWidthAndWillWrap(int width, int currentLineLength, ReadOnlySpan<char> chunk, int i)
            => chunk.Length > i + 1 && UnicodeWidth.GetWidth(chunk[i + 1]) > 1 && currentLineLength + 1 == width;
    }

    /// <summary>
    /// Wrap words into lines of at most maxLength long. Split on spaces
    /// where possible, otherwise split by character if a single word is
    /// greater than maxLength.
    /// </summary>
    public static List<FormattedString> WrapWords(FormattedString input, int maxLength, int? maxLines = null)
    {
        Debug.Assert(maxLength >= 0);
        Debug.Assert(maxLines.GetValueOrDefault() >= 0);

        if (input.Length == 0 || maxLength == 0 || maxLines == 0)
        {
            return new List<FormattedString>();
        }

        var lines = new List<FormattedString>();
        var currentLine = new FormattedStringBuilder();
        foreach (var line in input.Split('\n'))
        {
            currentLine.Clear();

            int currentLineWidth = 0;
            foreach (var word in line.Split(' '))
            {
                if (word.Length <= maxLength)
                {
                    if (!ProcessWord(word))
                    {
                        return lines;
                    }
                }
                else
                {
                    //slow path
                    foreach (var currentWord in word.SplitIntoChunks(maxLength))
                    {
                        if (!ProcessWord(currentWord))
                        {
                            return lines;
                        }
                    }
                }

                bool ProcessWord(FormattedString currentWord)
                {
                    var wordLength = currentWord.GetUnicodeWidth();
                    var wordWithSpaceLength = currentLineWidth == 0 ? wordLength : wordLength + 1;

                    bool result = true;
                    if (currentLineWidth > maxLength ||
                        currentLineWidth + wordWithSpaceLength > maxLength)
                    {
                        result = AddLine(currentLine.ToFormattedString());
                        currentLine.Clear();
                        currentLineWidth = 0;
                    }

                    if (currentLineWidth == 0)
                    {
                        currentLine.Append(currentWord);
                        currentLineWidth += wordLength;
                    }
                    else
                    {
                        currentLine.Append(" ");
                        currentLine.Append(currentWord);
                        currentLineWidth += wordLength + 1;
                    }
                    return result;
                }
            }

            if (!AddLine(currentLine.ToFormattedString()))
            {
                return lines;
            }
        }

        bool AddLine(FormattedString line)
        {
            if (lines.Count == maxLines)
            {
                var lastLine = lines[^1];
                FormattedString lastLineModified;
                lines.RemoveAt(lines.Count - 1);
                if (maxLength > 3)
                {
                    Debug.Assert(lastLine.GetUnicodeWidth() <= maxLength);
                    // reserve 3 columns for the ellipsis and truncate on a grapheme-cluster boundary by
                    // display width (maxLength is a column budget, not a character count).
                    lastLine = lastLine.Substring(0, UnicodeWidth.GetLengthThatFits(lastLine.Text, maxLength - 3));
                    lastLineModified = lastLine + "...";
                }
                else
                {
                    lastLineModified = new string('.', maxLength);
                }
                lines.Add(lastLineModified);
                return false;
            }

            lines.Add(line);
            return true;
        }

        return lines;
    }
}

internal struct WordWrappedText
{
    public IReadOnlyList<WrappedLine> WrappedLines { get; }
    private ConsoleCoordinate cursor;

    public ConsoleCoordinate Cursor
    {
        get => cursor;
        set
        {
            Debug.Assert(value.Row < WrappedLines.Count);
            Debug.Assert(value.Column <= WrappedLines[value.Row].Content.Length);
            cursor = value;
        }
    }

    public WordWrappedText(IReadOnlyList<WrappedLine> wrappedLines, ConsoleCoordinate cursor)
    {
        Debug.Assert(!wrappedLines[^1].Content.EndsWith('\n'));

        WrappedLines = wrappedLines;

        this.cursor = default;
        Cursor = cursor;
    }

    /// <summary>
    /// Recomputes the 2-D cursor coordinate for a 1-D <paramref name="caret"/> index from the already-wrapped
    /// lines, WITHOUT re-wrapping. Used on caret-only moves (see PERFORMANCE_PLAN.md Tier B1). This must produce
    /// the identical coordinate that <see cref="WordWrapping.WrapEditableCharacters"/> would compute for the same
    /// caret, text and width - a DEBUG assertion in <c>CodePane</c> verifies this on every caret move.
    /// </summary>
    public ConsoleCoordinate GetCursorForCaret(int caret)
    {
        Debug.Assert(caret >= 0);

        // Find the wrapped line containing the caret: the last line whose StartIndex is &lt;= caret. The boundary
        // rule (a caret exactly at a line's StartIndex belongs to THAT line at column 0) mirrors the wrap's
        // `isCursorPastCharacter = caret > textIndex` semantics at a line break (WordWrapping.cs).
        int lo = 0, hi = WrappedLines.Count - 1, row = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (WrappedLines[mid].StartIndex <= caret)
            {
                row = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // Column = count of non-control chars on the line before the caret. The only control char in wrapped
        // content is the line-terminating '\n', which is always the last char of its line and so never appears
        // strictly before the caret within the caret's own line - so this loop matches the wrap's per-char
        // `cursorColumn++` exactly (surrogate halves and combining marks each count as one, as they do there).
        var content = WrappedLines[row].Content;
        int offset = caret - WrappedLines[row].StartIndex;
        Debug.Assert(offset >= 0 && offset <= content.Length);
        int column = 0;
        for (int i = 0; i < offset; i++)
        {
            if (!char.IsControl(content[i]))
            {
                column++;
            }
        }

        return new ConsoleCoordinate(row, column);
    }
}

[DebuggerDisplay("{Content}")]
internal readonly struct WrappedLine
{
    public readonly int StartIndex;
    public readonly string Content;

    public WrappedLine(int startIndex, string content)
    {
        Debug.Assert(startIndex >= 0);

        StartIndex = startIndex;
        Content = content;
    }

    public static WrappedLine Empty(int startIndex) => new(startIndex, string.Empty);
}