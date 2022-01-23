#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Diagnostics;

namespace PrettyPrompt.Documents;

/// <summary>
/// A Document represents the input text being typed into the prompt.
/// It contains the text being typed, the caret/cursor positions, text selection, and word wrapping.
/// </summary>
[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
internal class Document : IEquatable<Document>
{
    private StringBuilderWithCaret stringBuilder;
    private readonly UndoRedoHistory undoRedoHistory;

    /// <summary>
    /// The one-dimensional index of the text caret in the document text
    /// </summary>
    public int Caret
    {
        get => stringBuilder.Caret;
        set => stringBuilder.Caret = value;
    }

    public Document() : this(string.Empty, 0) { }
    public Document(string text, int caret)
    {
        this.stringBuilder = new StringBuilderWithCaret(text, caret);
        this.undoRedoHistory = new UndoRedoHistory(stringBuilder);
    }

    public void InsertAtCaret(char character, TextSpan? selection)
    {
        undoRedoHistory.Track(stringBuilder);
        if (selection.TryGet(out var selectionValue))
        {
            DeleteSelectedText(selectionValue);
        }
        stringBuilder.Insert(Caret, character);
        undoRedoHistory.Track(stringBuilder);
    }

    public void DeleteSelectedText(TextSpan span)
    {
        undoRedoHistory.Track(stringBuilder);
        stringBuilder.Remove(span);
        undoRedoHistory.Track(stringBuilder);
    }

    public void InsertAtCaret(string text, TextSpan? selection)
    {
        undoRedoHistory.Track(stringBuilder);
        if (selection.TryGet(out var selectionValue))
        {
            DeleteSelectedText(selectionValue);
        }
        this.stringBuilder.Insert(Caret, text);
        undoRedoHistory.Track(stringBuilder);
    }

    public void Remove(TextSpan span) => Remove(span.Start, span.Length);

    public void Remove(int startIndex, int length)
    {
        if (startIndex >= stringBuilder.Length || startIndex < 0) return;

        undoRedoHistory.Track(stringBuilder);
        stringBuilder.Remove(startIndex, length);
        undoRedoHistory.Track(stringBuilder);
    }

    public void Clear()
    {
        undoRedoHistory.Track(stringBuilder);
        stringBuilder.Clear();
        undoRedoHistory.Track(stringBuilder);
    }

    public WordWrappedText WrapEditableCharacters(int width)
        => WordWrapping.WrapEditableCharacters(stringBuilder, Caret, width);

    public void MoveToWordBoundary(int direction) =>
        Caret = CalculateWordBoundaryIndexNearCaret(direction);

    public int CalculateWordBoundaryIndexNearCaret(int direction)
    {
        if (direction == 0) throw new ArgumentOutOfRangeException(nameof(direction), "cannot be 0");
        direction = Math.Sign(direction);

        if (direction > 0)
        {
            for (var i = Caret; i < stringBuilder.Length - 1; i++)
            {
                if (IsWordBoundary(i, i + 1))
                    return i + 1;
            }
            return stringBuilder.Length;
        }
        else
        {
            for (var i = Math.Min(Caret, stringBuilder.Length) - 1; i > 0; i--)
            {
                if (IsWordBoundary(i - 1, i))
                    return i;
            }
            return 0;
        }

        bool IsWordBoundary(int index1, int index2)
        {
            if (index2 >= stringBuilder.Length) return false;

            var c1 = stringBuilder[index1];
            var c2 = stringBuilder[index2];

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
            if (stringBuilder.Length == 0) return Caret;

            if (direction > 0)
            {
                for (var i = Caret; i < stringBuilder.Length; i++)
                {
                    if (stringBuilder[i] == '\n') return i;
                }
                return stringBuilder.Length;
            }
            else
            {
                //smart Home implementation (repeating Home presses switch between 'non-white-space start of line' and 'start of line')

                int lineStart = 0;
                var beforeCaretIndex = (Caret - 1).Clamp(0, Length - 1);
                for (int i = beforeCaretIndex; i >= 0; i--)
                {
                    if (stringBuilder[i] == '\n')
                    {
                        lineStart = Math.Min(i + 1, Length - 1);
                        break;
                    }
                }

                int lineStartNonWhiteSpace = lineStart;
                for (int i = lineStart; i < Length; i++)
                {
                    var c = stringBuilder[i];
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

    public Document Clone() => new(stringBuilder.ToString(), Caret);
    public void Undo() => stringBuilder = undoRedoHistory.Undo();
    public void Redo() => stringBuilder = undoRedoHistory.Redo();
    public void ClearUndoRedoHistory() => undoRedoHistory.Clear();

    /*
     * The following methods are forwarding along the StringBuilder APIs.
     */

    public char this[int index] => this.stringBuilder[index];
    public int Length => this.stringBuilder.Length;
    public string GetText() => this.stringBuilder.ToString();
    public string GetText(TextSpan span) => GetText(span.Start, span.Length);
    public string GetText(int startIndex, int length) => this.stringBuilder.ToString(startIndex, length);
    public bool StartsWith(string prefix) => prefix.Length <= this.stringBuilder.Length && this.stringBuilder.ToString(0, prefix.Length).Equals(prefix);
    public override bool Equals(object? obj) => Equals(obj as Document);
    public bool Equals(Document? other) => other != null && other.stringBuilder.Equals(this.stringBuilder);
    public override int GetHashCode() => this.stringBuilder.GetHashCode();
    private string GetDebuggerDisplay() => this.stringBuilder.ToString().Insert(this.Caret, "|");
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