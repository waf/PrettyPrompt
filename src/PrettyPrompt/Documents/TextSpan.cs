#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;

namespace PrettyPrompt.Documents;

//Microsoft.CodeAnalysis.Text.TextSpan equivalent.
public readonly struct TextSpan : IEquatable<TextSpan>
{
    /// <summary>
    /// Start point of the span.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Length of the span.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// End of the span.
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    /// Determines whether or not the span is empty.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Creates a TextSpan instance beginning with the position Start and having the Length specified with length.
    /// </summary>
    public TextSpan(int start, int length)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), "must be >=0");
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "must be >=0");

        Start = start;
        Length = length;
    }

    /// <summary>
    /// Creates a new TextSpan from start and end positions as opposed to a position and length.
    /// The returned TextSpan contains the range with start inclusive, and end exclusive.
    /// </summary>
    public static TextSpan FromBounds(int start, int end)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), "must be >=0");
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end), "must be >=start");

        return new TextSpan(start, end - start);
    }

    /// <summary>
    /// Determines whether the position lies within the span.
    /// </summary>
    public bool Contains(int index) => index >= Start && index < Start + Length;

    /// <summary>
    /// Determines whether span falls completely within this span.
    /// </summary>
    public bool Contains(TextSpan span) => span.Start >= Start && span.End <= End;

    /// <summary>
    /// Determines whether span overlaps this span. Two spans are considered to overlap if they have positions in common and neither is empty. Empty spans do not overlap with any other span.
    /// </summary>
    public bool OverlapsWith(TextSpan span) => OverlapsWith(span.Start, span.Length);

    /// <summary>
    /// Determines whether span overlaps this span. Two spans are considered to overlap if they have positions in common and neither is empty. Empty spans do not overlap with any other span.
    /// </summary>
    public bool OverlapsWith(int start, int length) => Math.Max(Start, start) < Math.Min(End, start + length);

    /// <summary>
    /// Returns the overlap with the given span, or null if there is no overlap.
    /// </summary>
    public TextSpan? Overlap(int start, int length)
    {
        int resultStart = Math.Max(Start, start);
        int resultEnd = Math.Min(End, start + length);
        if (resultStart < resultEnd)
        {
            return FromBounds(resultStart, resultEnd);
        }
        return null;
    }

    /// <summary>
    ///  Determines whether span intersects this span. Two spans are considered to intersect
    ///  if they have positions in common or the end of one span coincides with the start
    ///  of the other span.
    /// </summary>
    public bool IntersectsWith(TextSpan span) => span.Start <= End && span.End >= Start;

    /// <summary>
    /// Determines whether position intersects this span. A position is considered to
    /// intersect if it is between the start and end positions(inclusive) of this span.
    /// </summary>
    public bool IntersectsWith(int position) => (uint)(position - Start) <= (uint)Length;

    /// <summary>
    /// Returns the intersection with the given span, or null if there is no intersection.
    /// </summary>
    public TextSpan? Intersection(TextSpan span)
    {
        int resultStart = Math.Max(Start, span.Start);
        int resultEnd = Math.Min(End, span.End);
        if (resultStart <= resultEnd)
        {
            return FromBounds(resultStart, resultEnd);
        }
        return null;
    }

    public static bool operator ==(TextSpan left, TextSpan right) => left.Equals(right);
    public static bool operator !=(TextSpan left, TextSpan right) => !left.Equals(right);
    public bool Equals(TextSpan other) => Start == other.Start && Length == other.Length;
    public override bool Equals(object? obj) => obj is TextSpan other && Equals(other);
    public override int GetHashCode() => (Start, Length).GetHashCode();
    public override string ToString() => $"[{Start}..{End})";
}