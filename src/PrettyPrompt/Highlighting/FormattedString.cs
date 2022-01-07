#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using PrettyPrompt.Rendering;

namespace PrettyPrompt.Highlighting;

/// <summary>
/// Represents text with associated non-overlapping formating spans.
/// </summary>
public readonly struct FormattedString : IEquatable<FormattedString>
{
    public static FormattedString Empty => string.Empty;

    public string Text { get; }
    private readonly FormatSpan[] formatSpans;

    public IReadOnlyList<FormatSpan> FormatSpans => formatSpans ?? Array.Empty<FormatSpan>();
    public int Length => Text?.Length ?? 0;

    private string TextOrEmpty => Text ?? "";
    private FormatSpan[] FormatSpansOrEmpty => formatSpans ?? Array.Empty<FormatSpan>();

    public FormattedString(string text, IEnumerable<FormatSpan> formatSpans)
        : this(text, formatSpans?.ToArray())
    { }

    public FormattedString(string text, params FormatSpan[] formatSpans)
    {
        Text = text;
        if ((formatSpans?.Length ?? 0) == 0)
        {
            this.formatSpans = Array.Empty<FormatSpan>();
        }
        else
        {
            this.formatSpans = formatSpans.Where(s => s.Length > 0).OrderBy(s => s.Start).ToArray();
            CheckFormatSpans();
        }
    }

    public FormattedString(string text, ConsoleFormat formatting)
        : this(text, new FormatSpan(0, text.Length, formatting))
    {
    }

    public static implicit operator FormattedString(string text) => new(text);

    public static FormattedString operator +(FormattedString left, FormattedString right)
    {
        var resultText = left.TextOrEmpty + right.TextOrEmpty;

        var leftFormatSpans = left.FormatSpansOrEmpty;
        var rightFormatSpans = right.FormatSpansOrEmpty;
        var resultFormatSpans = new FormatSpan[leftFormatSpans.Length + rightFormatSpans.Length];
        leftFormatSpans.AsSpan().CopyTo(resultFormatSpans);
        for (int i = 0; i < rightFormatSpans.Length; i++)
        {
            resultFormatSpans[leftFormatSpans.Length + i] = rightFormatSpans[i].Offset(left.TextOrEmpty.Length);
        }

        return new FormattedString(resultText, resultFormatSpans);
    }

    public int GetUnicodeWidth() => UnicodeWidth.GetWidth(Text);

    /// <summary>
    /// Removes all leading and trailing white-space characters from the current string.
    /// </summary>
    public FormattedString Trim()
    {
        if (Text is null) return Empty;
        if (FormatSpansOrEmpty.Length == 0) return Text.Trim();

        var trimedCharsFromLeft = Text.Length - Text.AsSpan().TrimStart().Length;
        if (trimedCharsFromLeft == Text.Length) return Empty;

        var trimedCharsFromRight = Text.Length - Text.AsSpan().TrimEnd().Length;
        return Substring(trimedCharsFromLeft, Text.Length - trimedCharsFromLeft - trimedCharsFromRight);
    }

    /// <summary>
    /// Retrieves a substring from this instance. The substring starts at a specified character position and has a specified length.
    /// </summary>
    public FormattedString Substring(int startIndex, int length)
    {
        //formal argument validation will be done in Text.Substring(...)
        Debug.Assert(startIndex >= 0 && startIndex <= Length);
        Debug.Assert(length >= 0 && length - startIndex <= Length);

        if (Text is null || length == 0) return Empty;

        var substring = Text.Substring(startIndex, length);
        if (FormatSpansOrEmpty.Length == 0) return substring;

        var resultFormatSpans = new List<FormatSpan>(formatSpans.Length);
        foreach (var formatSpan in formatSpans)
        {
            if (formatSpan.Overlap(startIndex, length).TryGet(out var newSpan))
            {
                resultFormatSpans.Add(newSpan.Offset(-startIndex));
            }
        }

        return new(substring, resultFormatSpans);
    }

    /// <summary>
    /// Returns a new string in which all occurrences of a specified string in the current instance are replaced with another specified string.
    /// </summary>
    public FormattedString Replace(string oldValue, string newValue)
    {
        if (Text is null) return Empty;
        if (FormatSpansOrEmpty.Length == 0) return Text.Replace(oldValue, newValue);

        var text = Text.AsSpan();
        int currentOffsetInPartialyReplacedText = 0;

        var sb = new StringBuilder();
        var formatSpans = this.formatSpans.ToArray();
        int formatIndex = 0;
        while (true)
        {
            var replaceIndex = text.IndexOf(oldValue);
            if (replaceIndex < 0) break;

            sb.Append(text.Slice(0, replaceIndex));
            sb.Append(newValue);

            var replaceIndexInPartialyReplacedText = replaceIndex + currentOffsetInPartialyReplacedText;
            for (int i = formatIndex; i < formatSpans.Length; i++)
            {
                ref var formatSpan = ref formatSpans[i];
                if (replaceIndexInPartialyReplacedText >= formatSpan.End)
                {
                    //replace happens after current span, so we don't care about it anymore
                    Debug.Assert(i == formatIndex);
                    formatIndex++;
                }
                else if (formatSpan.OverlapsWith(start: replaceIndexInPartialyReplacedText, length: oldValue.Length))
                {
                    //replace overlaps with current span
                    if (replaceIndexInPartialyReplacedText >= formatSpan.Start && oldValue.Length >= formatSpan.Length)
                    {
                        //replace happens inside current span, so we just make the span shorter
                        formatSpan = formatSpan.WithLength(formatSpan.Length - oldValue.Length + newValue.Length);
                    }
                    else
                    {
                        //complex ovelap - we cannot decide what to do - throw the span away
                        formatSpan = FormatSpan.Empty;
                    }
                }
                else
                {
                    //replace happens before current span, so we just need to translate the whole span
                    formatSpan = formatSpan.Offset(newValue.Length - oldValue.Length);
                }
            }

            text = text.Slice(replaceIndex + oldValue.Length);
            currentOffsetInPartialyReplacedText += replaceIndex + newValue.Length;
        }

        sb.Append(text);

        return new FormattedString(sb.ToString(), formatSpans);
    }

    /// <summary>
    /// Splits a string into substrings based on the provided character separator.
    /// </summary>
    public IEnumerable<FormattedString> Split(char separator)
    {
        var text = Text;
        if (text is null) yield break;

        if (FormatSpansOrEmpty.Length == 0)
        {
            foreach (var part in text.Split(separator))
            {
                yield return part;
            }
        }
        else
        {
            int partStart = 0;
            var formattingList = new List<FormatSpan>(formatSpans.Length);
            int usedFormattingCount = 0;
            int previousFormattingCharsUsed = 0;
            while (partStart < text.Length)
            {
                int partLength = 0;
                for (int i = partStart; i < text.Length; i++, partLength++)
                {
                    if (text[i] == separator) break;
                }

                GenerateFormattingsForPart(partStart, partLength, partSeparatorLength: 1, ref usedFormattingCount, ref previousFormattingCharsUsed, formattingList);

                yield return new FormattedString(text.AsSpan(partStart, partLength).ToString(), formattingList);
                partStart += partLength + 1; //+1 to skip separator
            }
        }
    }

    public IEnumerable<FormattedString> SplitIntoChunks(int chunkSize)
    {
        if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize), "has to be >= 1");

        var text = Text;
        if (text is null) yield break;

        var stringWidth = UnicodeWidth.GetWidth(Text);
        if (stringWidth <= chunkSize)
        {
            yield return this;
            yield break;
        }

        int partStart = 0;
        var formattingList = new List<FormatSpan>(formatSpans.Length);
        int usedFormattingCount = 0;
        int previousFormattingCharsUsed = 0;
        while (partStart < text.Length)
        {
            int partLength = 0;
            for (int i = partStart, partWidth = 0; i < text.Length; i++, partLength++)
            {
                var cWidth = UnicodeWidth.GetWidth(text[i]);
                partWidth += cWidth;
                if (partWidth > chunkSize) break;
            }

            GenerateFormattingsForPart(partStart, partLength, partSeparatorLength: 0, ref usedFormattingCount, ref previousFormattingCharsUsed, formattingList);

            yield return new FormattedString(text.AsSpan(partStart, partLength).ToString(), formattingList);
            partStart += partLength;
        }
    }

    private void GenerateFormattingsForPart(
        int partStart,
        int partLength,
        int partSeparatorLength,
        ref int usedFormattingCount,
        ref int previousFormattingCharsUsed,
        List<FormatSpan> formattingList)
    {
        formattingList.Clear();

        var partEnd = partStart + partLength;
        for (int i = usedFormattingCount; i < formatSpans.Length; i++)
        {
            var formatting = formatSpans[i];
            if (formatting.Start >= partEnd)
            {
                //no more formattings for this part
                break;
            }

            Debug.Assert(previousFormattingCharsUsed < formatting.Length);

            if (formatting.End <= partStart)
            {
                //formatting ended before this part
                previousFormattingCharsUsed = 0;
                usedFormattingCount++;
                continue;
            }

            var offset = -Math.Min(formatting.Start, partStart);
            var hasValue = formatting
                .Offset(offset) //has to be relative to strat of current part
                .WithLength(formatting.Length - previousFormattingCharsUsed) //some chars could be already used by previous parts
                .Overlap(0, partLength)
                .TryGet(out var newFormatting);

            Debug.Assert(hasValue, "formatting has to overlap due to prior conditions");
            formattingList.Add(newFormatting);

            if (formatting.End <= partEnd + partSeparatorLength)
            {
                //formatting cannot affect next part
                previousFormattingCharsUsed = 0;
                usedFormattingCount++;
            }
            else
            {
                previousFormattingCharsUsed += newFormatting.Length + partSeparatorLength;
            }
        }
    }

    public IEnumerable<(string Element, ConsoleFormat Formatting)> EnumerateTextElements()
    {
        var enumerator = StringInfo.GetTextElementEnumerator(TextOrEmpty);
        int textIndex = 0;
        int formatIndex = 0;
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();

            ConsoleFormat formatting;
            if (formatIndex < formatSpans.Length)
            {
                var span = formatSpans[formatIndex];
                if (span.Contains(textIndex))
                {
                    formatting = span.Formatting;
                }
                else
                {
                    formatting = ConsoleFormat.None;
                }
            }
            else
            {
                formatting = ConsoleFormat.None;
            }

            yield return (element, formatting);

            textIndex += element.Length;
            if (formatIndex < formatSpans.Length &&
                textIndex >= formatSpans[formatIndex].End)
            {
                formatIndex++;
            }
        }
    }

    private void CheckFormatSpans()
    {
        var textLen = Length;
        if (textLen == 0)
        {
            if (formatSpans.Length != 0) throw new ArgumentException("There is no text to be formatted.", nameof(formatSpans));
        }
        else
        {
            for (int i = 0; i < formatSpans.Length; i++)
            {
                var span = formatSpans[i];
                if (span.Start >= textLen) throw new ArgumentException("Span start cannot be larger than text length.", nameof(formatSpans));
                if (span.Start + span.Length > textLen) throw new ArgumentException("Span end cannot be outside of text.", nameof(formatSpans));

                if (i > 0)
                {
                    var previousSpan = formatSpans[i - 1];
                    if (span.Start < previousSpan.End) throw new ArgumentException("Spans cannot overlap.", nameof(formatSpans));
                }
            }
        }
    }

    public bool Equals(FormattedString other)
    {
        if (Text != other.Text) return false;
        if (FormatSpans.Count != other.FormatSpans.Count) return false;
        for (int i = 0; i < FormatSpans.Count; i++)
        {
            if (FormatSpans[i] != other.FormatSpans[i]) return false;
        }
        return true;
    }

    public override bool Equals(object obj) => obj is FormattedString other && Equals(other);
    public override int GetHashCode() => string.GetHashCode(Text);
    public override string ToString() => Text;

    public static bool operator ==(FormattedString left, FormattedString right) => left.Equals(right);
    public static bool operator !=(FormattedString left, FormattedString right) => !(left == right);
}
