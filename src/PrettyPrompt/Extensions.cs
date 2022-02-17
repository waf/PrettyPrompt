#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using PrettyPrompt.Documents;

namespace PrettyPrompt;

public static class Extensions
{
    internal static string EnvironmentNewlines(this string text) =>
        Environment.NewLine == "\n"
            ? text
            : text.Replace("\n", Environment.NewLine);

    /// <summary>
    /// Like <see cref="System.Linq.Enumerable.Zip{TFirst, TSecond}(IEnumerable{TFirst}, IEnumerable{TSecond})"/>,
    /// but the length of the zipped sequence is equal to the longer enumerable (with default(T) elements for the shorter enumerable).
    /// </summary>
    internal static IEnumerable<(int, T1?, T2?)> ZipLongest<T1, T2>(this IEnumerable<T1> left, IEnumerable<T2> right)
    {
        var leftEnumerator = left.GetEnumerator();
        var rightEnumerator = right.GetEnumerator();

        bool hasLeft = leftEnumerator.MoveNext();
        bool hasRight = rightEnumerator.MoveNext();

        int i = 0;
        while (hasLeft || hasRight)
        {
            if (hasLeft && hasRight)
            {
                yield return (i, leftEnumerator.Current, rightEnumerator.Current);
            }
            else if (hasLeft)
            {
                yield return (i, leftEnumerator.Current, default);
            }
            else if (hasRight)
            {
                yield return (i, default, rightEnumerator.Current);
            }

            hasLeft = leftEnumerator.MoveNext();
            hasRight = rightEnumerator.MoveNext();
            i++;
        }
    }

    internal static bool TryGet<T>(this T? nullableValue, out T value)
        where T : struct
    {
        if (nullableValue.HasValue)
        {
            value = nullableValue.GetValueOrDefault();
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    internal static ConsoleKeyInfo ToKeyInfo(this ConsoleKey consoleKey, char character, ConsoleModifiers modifiersPressed)
       => consoleKey.ToKeyInfo(
           character,
           shift: modifiersPressed.HasFlag(ConsoleModifiers.Shift),
           alt: modifiersPressed.HasFlag(ConsoleModifiers.Alt),
           control: modifiersPressed.HasFlag(ConsoleModifiers.Control));

    internal static ConsoleKeyInfo ToKeyInfo(this ConsoleKey consoleKey, char character, bool shift = false, bool alt = false, bool control = false)
       => new(character, consoleKey, shift, alt, control);

    internal static int Clamp(this int value, int min, int max)
    {
        Debug.Assert(min <= max);
        return value < min ? min : (value > max ? max : value);
    }

    internal static void SetContents(this StringBuilder sb, string contents)
    {
        sb.Clear();
        sb.Append(contents);
    }

    internal static bool StartsWith(this string text, ReadOnlyStringBuilder prefix)
    {
        if (prefix.Length > text.Length) return false;
        for (int i = 0; i < prefix.Length; i++)
        {
            if (text[i] != prefix[i]) return false;
        }
        return true;
    }

    public static ReadOnlySpan<char> AsSpan(this string text, TextSpan span)
        => text.AsSpan(span.Start, span.Length);
}