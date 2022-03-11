#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Diagnostics;
using PrettyPrompt.Panes;
using PrettyPrompt.TextSelection;

namespace PrettyPrompt.Documents;

/// <summary>
/// A Document represents the input text being typed into the prompt.
/// It contains the text being typed, the caret/cursor positions, text selection, and word wrapping.
/// </summary>
[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
internal class Document : IEquatable<Document>
{
    private readonly StringBuilderWithCaret stringBuilder;
    private readonly UndoRedoHistory undoRedoHistory;

    /// <summary>
    /// Cached ToStringed current stringBuilder content. This is optimization for GetText calls so they don't repeatedly ToString underlying stringBuilder.
    /// </summary>
    private string currentText;

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
        this.undoRedoHistory = new UndoRedoHistory(text, caret);
        this.currentText = text;
        stringBuilder.TextChanged += () => currentText = stringBuilder.ToString();
    }

    public void InsertAtCaret(CodePane codePane, char character)
    {
        using (BeginChanges(codePane))
        {
            if (codePane.GetSelectionSpan().TryGet(out var selectionValue))
            {
                codePane.Selection = null;
                stringBuilder.Remove(selectionValue);
            }
            stringBuilder.Insert(Caret, character);
        }
    }

    public void DeleteSelectedText(CodePane codePane)
    {
        using (BeginChanges(codePane))
        {
            if (codePane.GetSelectionSpan().TryGet(out var selectionValue))
            {
                codePane.Selection = null;
                stringBuilder.Remove(selectionValue);
            }
        }
    }

    public void InsertAtCaret(CodePane codePane, string text)
    {
        using (BeginChanges(codePane))
        {
            if (codePane.GetSelectionSpan().TryGet(out var selectionValue))
            {
                stringBuilder.Remove(selectionValue);
            }
            this.stringBuilder.Insert(Caret, text);
        }
    }

    public void Remove(CodePane codePane, TextSpan span) => Remove(codePane, span.Start, span.Length);

    public void Remove(CodePane codePane, int startIndex, int length)
    {
        if (startIndex >= stringBuilder.Length || startIndex < 0) return;

        using (BeginChanges(codePane))
        {
            stringBuilder.Remove(startIndex, length);
        }
    }

    public void Clear(CodePane codePane)
    {
        using (BeginChanges(codePane))
        {
            stringBuilder.Clear();
        }
    }

    public void SetContents(CodePane codePane, string contents)
    {
        using (BeginChanges(codePane))
        {
            stringBuilder.SetContents(contents);
        }
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

    public void Undo(out SelectionSpan? selection)
    {
        var record = undoRedoHistory.Undo();
        selection = record.Selection;
        stringBuilder.SetContents(record.Text);
        Caret = record.Caret;
    }

    public void Redo(out SelectionSpan? selection)
    {
        var record = undoRedoHistory.Redo();
        selection = record.Selection;
        stringBuilder.SetContents(record.Text);
        Caret = record.Caret;
    }

    public void ClearUndoRedoHistory() => undoRedoHistory.Clear();

    public event Action? Changed
    {
        add => stringBuilder.Changed += value;
        remove => stringBuilder.Changed -= value;
    }

    /*
     * The following methods are forwarding along the StringBuilder APIs.
     */
    public char this[int index] => currentText[index];
    public int Length => currentText.Length;
    public string GetText() => currentText;
    public ReadOnlySpan<char> GetText(TextSpan span) => currentText.AsSpan(span);
    public override bool Equals(object? obj) => Equals(obj as Document);
    public bool Equals(Document? other) => other != null && other.currentText.Equals(currentText);
    public override int GetHashCode() => currentText.GetHashCode();
    private string GetDebuggerDisplay() => currentText.Insert(this.Caret, "|");

    /// <summary>
    /// Accumulates changed events and invokes only one on dispose.
    /// Also takes care of history tracking (before/after).
    /// </summary>
    private ChangeContext BeginChanges(CodePane codePane) => new(codePane, this);

    private readonly struct ChangeContext : IDisposable
    {
        private readonly CodePane codePane;
        private readonly Document document;

        public ChangeContext(CodePane codePane, Document document)
        {
            Debug.Assert(document.stringBuilder.ToString() == document.currentText);

            this.codePane = codePane;
            this.document = document;
            document.stringBuilder.SuspendChangedEvents();
            document.undoRedoHistory.Track(document.stringBuilder, document.Caret, codePane.Selection);
        }

        public void Dispose()
        {
            document.stringBuilder.ResumeChangedEvents();
            document.undoRedoHistory.Track(document.stringBuilder, document.Caret, codePane.Selection);

            Debug.Assert(document.stringBuilder.ToString() == document.currentText);
        }
    }
}