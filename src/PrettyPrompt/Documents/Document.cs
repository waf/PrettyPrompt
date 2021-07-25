#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;
using PrettyPrompt.TextSelection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PrettyPrompt.Documents
{
    /// <summary>
    /// A Document represents the input text being typed into the prompt.
    /// It contains the text being typed, the caret/cursor positions, text selection, and word wrapping.
    /// </summary>
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal class Document : IEquatable<Document>
    {
        private readonly StringBuilder text;
        private readonly UndoRedoHistory undoRedoHistory;

        /// <summary>
        /// The one-dimensional index of the text caret in the document text
        /// </summary>
        public int Caret { get; set; }

        /// <summary>
        /// The two-dimensional coordinate of the text cursor in the document,
        /// after word wrapping / newlines have been processed.
        /// </summary>
        public ConsoleCoordinate Cursor { get; private set; }

        /// <summary>
        /// After <see cref="WordWrap(int)"/> is called, this will contain the
        /// text split into lines.
        /// </summary>
        public IReadOnlyList<WrappedLine> WordWrappedLines { get; private set; }

        public List<SelectionSpan> Selection { get; } = new();


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
            if (Selection.Count == 0) return;

            undoRedoHistory.Track(this);
            int firstSelectionStart = 0;
            for (int i = Selection.Count - 1; i >= 0; i--)
            {
                var (start, end) = Selection[i].GetCaretIndices(WordWrappedLines);
                text.Remove(start, end - start);
                firstSelectionStart = start;
            }

            Caret = firstSelectionStart;
            undoRedoHistory.Track(this);
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
            int bound = direction > 0 ? text.Length : 0;

            if (Math.Abs(Caret - bound) <= 2)
                return bound;

            for (var i = Caret + direction; bound == 0 ? i > 0 : i < bound - 1; i += direction)
            {
                int c1Index = i + (direction > 0 ? 0 : -1);
                int c2Index = i + (direction > 0 ? 1 : 0);
                if (IsWordStart(text[c1Index], text[c2Index]))
                    return c2Index;
            }

            static bool IsWordStart(char c1, char c2) => !char.IsLetterOrDigit(c1) && char.IsLetterOrDigit(c2);

            return bound;
        }

        public void MoveToLineBoundary(int direction) =>
            Caret = CalculateLineBoundaryIndexNearCaret(direction);

        public int CalculateLineBoundaryIndexNearCaret(int direction)
        {
            if (text.Length == 0) return Caret;

            if (direction == +1 && Caret < text.Length && text[Caret] == '\n') return Caret;

            int bound = direction > 0 ? text.Length : 0;

            for (var i = Caret + direction; bound == 0 ? i > 0 : i < bound; i += direction)
            {
                if (text[i] == '\n')
                    return i + (direction == -1 ? 1 : 0);
            }

            return bound;
        }

        public Document Clone() => new Document(text.ToString(), Caret);

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

    public record WrappedLine(int StartIndex, string Content);
}
