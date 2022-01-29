#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
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
    private bool changedEventsSuspended;
    private bool changedDuringEventSuspension;

    public event Action? Changed;

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
            InvokeChangedEvent();
        }
    }

    public StringBuilderWithCaret() : this(string.Empty, 0) { }
    public StringBuilderWithCaret(string text, int caret)
    {
        sb = new StringBuilder(text);
        Caret = caret;
    }

    public static implicit operator ReadOnlyStringBuilder(StringBuilderWithCaret sb) => new(sb.sb);

    public char this[int index] => sb[index];
    public int Length => sb.Length;

    public void Clear()
    {
        if (sb.Length > 0)
        {
            Caret = 0;
            sb.Clear();
            InvokeChangedEvent();
        }
    }

    public void SetContents(string contents)
    {
        sb.SetContents(contents);
        Caret = sb.Length;
        InvokeChangedEvent();
    }

    public void Insert(int index, char c)
    {
        sb.Insert(index, c);
        ++Caret;
        InvokeChangedEvent();
    }

    public void Insert(int index, string text)
    {
        sb.Insert(index, text);
        Caret += text.Length;
        InvokeChangedEvent();
    }

    public void Remove(int startIndex, int length)
    {
        sb.Remove(startIndex, length);
        Caret = startIndex;
        InvokeChangedEvent();
    }

    public void Remove(TextSpan span) => Remove(span.Start, span.Length);
    public StringBuilder.ChunkEnumerator GetChunks() => sb.GetChunks();
    public override string ToString() => sb.ToString();
    public string ToString(int startIndex, int length) => sb.ToString(startIndex, length);
    public bool EqualsText(StringBuilderWithCaret other) => sb.Equals(other.sb);
    private string GetDebuggerDisplay() => sb.ToString().Insert(Caret, "|");

    public void SuspendChangedEvents()
    {
        changedEventsSuspended = true;
        changedDuringEventSuspension = false;
    }

    public void ResumeChangedEvents()
    {
        changedEventsSuspended = false;
        if (changedDuringEventSuspension)
        {
            InvokeChangedEvent();
        }
    }

    private void InvokeChangedEvent()
    {
        if (changedEventsSuspended)
        {
            changedDuringEventSuspension = true;
        }
        else
        {
            Changed?.Invoke();
        }
    }
}