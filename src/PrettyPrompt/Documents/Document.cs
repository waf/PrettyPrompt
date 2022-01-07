#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using PrettyPrompt.Consoles;
using PrettyPrompt.TextSelection;

namespace PrettyPrompt.Documents;

/// <summary>
/// A Document represents the input text being typed into the prompt.
/// It contains the text being typed, the caret/cursor positions, text selection, and word wrapping.
/// </summary>
[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
internal class Document : IEquatable<Document>
{
    private readonly StringBuilder text;
    private readonly UndoRedoHistory undoRedoHistory;
    private int caret;

    /// <summary>
    /// The one-dimensional index of the text caret in the document text
    /// </summary>
    public int Caret
    {
        get => caret;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= text.Length);
            caret = value;
        }
    }

    /// <summary>
    /// The two-dimensional coordinate of the text cursor in the document,
    /// after word wrapping / newlines have been processed.
    /// </summary>
    public ConsoleCoordinate Cursor { get; set; }

    /// <summary>
    /// After <see cref="WordWrap(int)"/> is called, this will contain the
    /// text split into lines.
    /// </summary>
    public IReadOnlyList<WrappedLine> WordWrappedLines { get; private set; }

    public SelectionSpan? Selection { get; set; }

    public Document() : this(string.Empty, 0) { }
    public Document(string text, int caret)
    {
        this.text = new StringBuilder(text);
        this.Caret = caret;
        this.undoRedoHistory = new UndoRedoHistory();
    }

    public void InsertAtCaret(char character)
    {
        undoRedoHistory.Track(this);
        DeleteSelectedText();
        text.Insert(Caret, character);
        Caret++;
        undoRedoHistory.Track(this);
    }

    public void DeleteSelectedText()
    {
        if (Selection.TryGet(out var selection))
        {
            undoRedoHistory.Track(this);
            var (start, end) = selection.GetCaretIndices(WordWrappedLines);
            text.Remove(start, end - start);

            Caret = start;
            undoRedoHistory.Track(this);
        }
    }

    public void InsertAtCaret(string text)
    {
        undoRedoHistory.Track(this);
        DeleteSelectedText();
        this.text.Insert(Caret, text);
        Caret += text.Length;
        undoRedoHistory.Track(this);
    }

    public void Remove(int startIndex, int length)
    {
        if (startIndex >= text.Length || startIndex < 0) return;

        undoRedoHistory.Track(this);
        text.Remove(startIndex, length);
        Caret = startIndex;
        undoRedoHistory.Track(this);
    }

    public void Clear()
    {
        undoRedoHistory.Track(this);
        text.Clear();
        Caret = 0;
        undoRedoHistory.Track(this);
    }

    public void WordWrap(int width)
    {
        (WordWrappedLines, Cursor) = WordWrapping.WrapEditableCharacters(text, Caret, width);
    }

    public void MoveToWordBoundary(int direction) =>
        Caret = CalculateWordBoundaryIndexNearCaret(direction);

    public int CalculateWordBoundaryIndexNearCaret(int direction)
    {
        if (direction == 0) throw new ArgumentOutOfRangeException(nameof(direction), "cannot be 0");
        direction = Math.Sign(direction);

        if (direction > 0)
        {
            for (var i = Caret; i < text.Length - 1; i++)
            {
                if (IsWordBoundary(i, i + 1))
                    return i + 1;
            }
            return text.Length;
        }
        else
        {
            for (var i = Math.Min(Caret, text.Length) - 1; i > 0; i--)
            {
                if (IsWordBoundary(i - 1, i))
                    return i;
            }
            return 0;
        }

        bool IsWordBoundary(int index1, int index2)
        {
            if (index2 >= text.Length) return false;

            var c1 = text[index1];
            var c2 = text[index2];

            var isWhitespace1 = char.IsWhiteSpace(c1);
            var isWhitespace2 = char.IsWhiteSpace(c2);
            if (isWhitespace1 && !isWhitespace2) return true;
            if (isWhitespace1 || isWhitespace2) return false;

            return char.IsLetterOrDigit(c1) != char.IsLetterOrDigit(c2);
        }
    }

    public void MoveToLineBoundary(int direction)
    {
        Debug.Assert(direction is -1 or 1);

        Caret = CalculateLineBoundaryIndexNearCaret(direction);

        int CalculateLineBoundaryIndexNearCaret(int direction)
        {
            if (text.Length == 0) return Caret;

            if (direction > 0)
            {
                for (var i = Caret; i < text.Length; i++)
                {
                    if (text[i] == '\n') return i;
                }
                return text.Length;
            }
            else
            {
                //smart Home implementation (repeating Home presses switch between 'non-white-space start of line' and 'start of line')

                int lineStart = 0;
                var beforeCaretIndex = (Caret - 1).Clamp(0, Length - 1);
                for (int i = beforeCaretIndex; i >= 0; i--)
                {
                    if (text[i] == '\n')
                    {
                        lineStart = Math.Min(i + 1, Length - 1);
                        break;
                    }
                }

                int lineStartNonWhiteSpace = lineStart;
                for (int i = lineStart; i < Length; i++)
                {
                    var c = text[i];
                    if (c == '\n')
                    {
                        return lineStart;
                    }
                    if (!char.IsWhiteSpace(c))
                    {
                        lineStartNonWhiteSpace = i;
                        break;
                    }
                }

                return lineStartNonWhiteSpace == beforeCaretIndex + 1 ? lineStart : lineStartNonWhiteSpace;
            }
        }
    }

    public Document Clone() => new(text.ToString(), Caret);

    public void Undo()
    {
        var newVersion = undoRedoHistory.Undo();
        this.text.Clear();
        this.text.Append(newVersion.text);
        this.Caret = newVersion.Caret;
    }

    public void Redo()
    {
        var newVersion = undoRedoHistory.Redo();
        this.text.Clear();
        this.text.Append(newVersion.text);
        this.Caret = newVersion.Caret;
    }

    public void ClearUndoRedoHistory() => undoRedoHistory.Clear();

    /*
     * The following methods are forwarding along the StringBuilder APIs.
     */

    public char this[int index] => this.text[index];
    public int Length => this.text.Length;
    public string GetText() => this.text.ToString();
    public string GetText(int startIndex, int length) => this.text.ToString(startIndex, length);
    public bool StartsWith(string prefix) =>
        prefix.Length <= this.text.Length && this.text.ToString(0, prefix.Length).Equals(prefix);
    public override bool Equals(object obj) => Equals(obj as Document);
    public bool Equals(Document other) => other != null && other.text.Equals(this.text);
    public override int GetHashCode() => this.text.GetHashCode();
    private string GetDebuggerDisplay() => this.text.ToString().Insert(this.Caret, "|");
}

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
}