#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Diagnostics;

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

    private bool textChangedDuringEventSuspension;
    private bool caretChangedDuringEventSuspension;

    public event Action? Changed;
    public event Action? TextChanged;

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
            InvokeChangedEvent(caretOnly: true);
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
            sb.Clear();
            // Assign the caret field directly rather than via the Caret setter: the setter raises a transient
            // caret-only Changed event mid-mutation (text already changed, but no TextChanged announced yet),
            // which would drive consumers to recompute the cursor against now-stale state. The single
            // InvokeChangedEvent below notifies once, with text and caret already consistent.
            caret = 0;
            InvokeChangedEvent();
        }
    }

    public void SetContents(string contents, int? caret = null)
    {
        sb.SetContents(contents);
        // Assign the caret field directly (not via the Caret setter) to avoid a transient caret-only Changed
        // event before the text change is announced - see the note in Clear().
        this.caret = caret ?? sb.Length;
        Debug.Assert(this.caret >= 0 && this.caret <= sb.Length);
        InvokeChangedEvent();
    }

    public void Insert(int index, char c)
    {
        sb.Insert(index, c);
        // Advance the caret via the field, not the Caret setter, so we don't raise a transient caret-only
        // Changed event before the text change is announced (see the note in Clear()). The single
        // InvokeChangedEvent below fires once, with text and caret already consistent - which matters for
        // callers that update live without suspending events (e.g. streaming via InsertAtCaretAsync).
        caret++;
        Debug.Assert(caret >= 0 && caret <= sb.Length);
        InvokeChangedEvent();
    }

    public void Insert(int index, ReadOnlySpan<char> text)
    {
        sb.Insert(index, text);
        caret += text.Length; // assign the field directly, not the Caret setter - see the note in Insert(int, char).
        Debug.Assert(caret >= 0 && caret <= sb.Length);
        InvokeChangedEvent();
    }

    public void Remove(int startIndex, int length)
    {
        sb.Remove(startIndex, length);
        caret = startIndex; // assign the field directly, not the Caret setter - see the note in Insert(int, char).
        Debug.Assert(caret >= 0 && caret <= sb.Length);
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
        textChangedDuringEventSuspension = false;
        caretChangedDuringEventSuspension = false;
    }

    public void ResumeChangedEvents()
    {
        changedEventsSuspended = false;
        if (textChangedDuringEventSuspension || caretChangedDuringEventSuspension)
        {
            InvokeChangedEvent(caretOnly: !textChangedDuringEventSuspension);
        }
    }

    private void InvokeChangedEvent(bool caretOnly = false)
    {
        if (changedEventsSuspended)
        {
            if (!caretOnly)
            {
                textChangedDuringEventSuspension = true;
            }
            caretChangedDuringEventSuspension = true;
        }
        else
        {
            if (!caretOnly)
            {
                TextChanged?.Invoke();
            }
            Changed?.Invoke();
        }
    }
}