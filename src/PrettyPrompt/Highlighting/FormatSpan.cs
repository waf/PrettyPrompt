#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;

namespace PrettyPrompt.Highlighting;

public readonly record struct FormatSpan
{
    public static readonly FormatSpan Empty = new(0, 0, ConsoleFormat.None);

    public int Start { get; }
    public int Length { get; }
    public ConsoleFormat Formatting { get; }

    public FormatSpan(
       int start,
       int length,
       ConsoleFormat formatting)
    {
        if (start < 0) throw new ArgumentException("Cannot be negative.", nameof(start));
        if (length < 0) throw new ArgumentException("Cannot be negative.", nameof(length));

        Start = start;
        Length = length;
        Formatting = formatting;
    }

    public static FormatSpan FromBounds(int start, int end, ConsoleFormat formatting) => new(start, end - start, formatting);

    /// <summary>
    /// Determines whether the position lies within the span.
    /// </summary>
    public bool Contains(int index) => index >= Start && index < Start + Length;

    /// <summary>
    /// The end of the span. The span is open-ended on the right side, which is to say that Start + Length = End.
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    /// Creates new span translated by some offset.
    /// </summary>
    public FormatSpan Offset(int offset) => new(Start + offset, Length, Formatting);

    /// <summary>
    /// Creates new span with new length.
    /// </summary>
    public FormatSpan WithLength(int length) => new(Start, length, Formatting);

    /// <summary>
    /// Determines whether span overlaps this span. Two spans are considered to overlap if they have positions in common and neither is empty. Empty spans do not overlap with any other span.
    /// </summary>
    public bool OverlapsWith(FormatSpan span) => OverlapsWith(span.Start, span.Length);

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
}