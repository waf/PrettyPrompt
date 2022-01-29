#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Text;

namespace PrettyPrompt.Documents;

/// <summary>
/// A Document represents the input text being typed into the prompt.
/// It contains the text being typed, the caret/cursor positions, text selection, and word wrapping.
/// </summary>
internal readonly struct ReadOnlyStringBuilder
{
    private readonly StringBuilder sb;

    public ReadOnlyStringBuilder(StringBuilder sb) => this.sb = sb;

    public char this[int index] => sb[index];
    public int Length => sb.Length;

    public static implicit operator ReadOnlyStringBuilder(StringBuilder sb) => new(sb);

    public override string ToString() => sb.ToString();
    public string ToString(int startIndex, int length) => sb.ToString(startIndex, length);

    public void AppendTo(StringBuilder other) => other.Append(sb);

    public bool Equals(ReadOnlyStringBuilder other) => sb.Equals(other.sb);
    public bool Equals(ReadOnlyStringBuilder? other) => other.TryGet(out var otherValue) && sb.Equals(otherValue.sb);
    public bool Equals(string other) => sb.Equals(other);

    public bool ReferenceEquals(ReadOnlyStringBuilder other) => sb == other.sb;
}