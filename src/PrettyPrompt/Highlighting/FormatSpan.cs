#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using PrettyPrompt.Documents;

namespace PrettyPrompt.Highlighting;

public readonly record struct FormatSpan
{
    public static readonly FormatSpan Empty = new(0, 0, ConsoleFormat.None);

    public readonly TextSpan Span;
    public readonly ConsoleFormat Formatting;

    /// <summary>
    /// Start point of the span.
    /// </summary>
    public int Start => Span.Start;

    /// <summary>
    /// Length of the span.
    /// </summary>
    public int Length => Span.Length;

    /// <summary>
    /// End of the span.
    /// </summary>
    public int End => Span.End;

    /// <summary>
    /// Determines whether or not the span is empty.
    /// </summary>
    public bool IsEmpty => Span.IsEmpty;

    public FormatSpan(
      TextSpan span,
      ConsoleFormat formatting)
    {
        Span = span;
        Formatting = formatting;
    }

    public FormatSpan(
       int start,
       int length,
       ConsoleFormat formatting)
        : this(new TextSpan(start, length), formatting)
    { }

    public FormatSpan(
      int start,
      int length,
      AnsiColor foregroundColor)
        : this(start, length, new ConsoleFormat(Foreground: foregroundColor))
    { }

    public static FormatSpan FromBounds(int start, int end, ConsoleFormat formatting) => new(TextSpan.FromBounds(start, end), formatting);

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
    public FormatSpan? Overlap(int start, int length)
    {
        int resultStart = Math.Max(Start, start);
        int resultEnd = Math.Min(End, start + length);
        if (resultStart < resultEnd)
        {
            return FromBounds(resultStart, resultEnd, Formatting);
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
    public FormatSpan? Intersection(TextSpan span)
    {
        int resultStart = Math.Max(Start, span.Start);
        int resultEnd = Math.Min(End, span.End);
        if (resultStart <= resultEnd)
        {
            return FromBounds(resultStart, resultEnd, Formatting);
        }
        return null;
    }

    /// <summary>
    /// Creates new span translated by some offset.
    /// </summary>
    public FormatSpan Offset(int offset) => new(Start + offset, Length, Formatting);

    /// <summary>
    /// Creates new span with new length.
    /// </summary>
    public FormatSpan WithLength(int length) => new(Start, length, Formatting);

    public override string ToString() => $"[{Start}..{End})";

    public bool Equals(in FormatSpan other)
    {
        //struct is big so we use custom by-ref equals
        return
            Span == other.Span &&
            Formatting.Equals(in other.Formatting);
    }
}