#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Diagnostics;
using System.Text;

namespace PrettyPrompt.Documents;

/// <summary>
/// A Document represents the input text being typed into the prompt.
/// It contains the text being typed, the caret/cursor positions, text selection, and word wrapping.
/// </summary>
[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
internal class StringBuilderWithCaret
{
    private readonly StringBuilder sb;
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
            Debug.Assert(value <= sb.Length);
            caret = value;
        }
    }

    public StringBuilderWithCaret() : this(string.Empty, 0) { }
    public StringBuilderWithCaret(string text, int caret)
    {
        sb = new StringBuilder(text);
        Caret = caret;
    }

    public char this[int index] => sb[index];
    public int Length => sb.Length;

    public void Clear()
    {
        Caret = 0;
        sb.Clear();
    }

    public void Insert(int index, char c)
    {
        sb.Insert(index, c);
        ++Caret;
    }

    public void Insert(int index, string text)
    {
        sb.Insert(index, text);
        Caret += text.Length;
    }

    public void Remove(int startIndex, int length)
    {
        sb.Remove(startIndex, length);
        Caret = startIndex;
    }

    public void Remove(TextSpan span) => Remove(span.Start, span.Length);
    public StringBuilder.ChunkEnumerator GetChunks() => sb.GetChunks();
    public override string ToString() => sb.ToString();
    public string ToString(int startIndex, int length) => sb.ToString(startIndex, length);
    public bool EqualsText(StringBuilderWithCaret other) => sb.Equals(other.sb);
    public StringBuilderWithCaret Clone() => new(sb.ToString(), caret);
    private string GetDebuggerDisplay() => sb.ToString().Insert(Caret, "|");
}