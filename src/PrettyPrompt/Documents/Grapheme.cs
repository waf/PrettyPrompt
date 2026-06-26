#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Globalization;

namespace PrettyPrompt.Documents;

/// <summary>
/// Helpers for moving between grapheme-cluster boundaries in a string.
///
/// The document caret is a UTF-16 offset, but it should only ever rest on a grapheme-cluster
/// boundary so that cursor movement and deletion operate on whole user-perceived characters -
/// e.g. an emoji such as "🤦🏼‍♂️" (one cluster, seven <see cref="char"/>s) or a base letter plus
/// combining marks - rather than splitting them into halves of a surrogate pair or orphaned
/// combining marks. See https://github.com/waf/PrettyPrompt/issues/270.
///
/// <para>
/// Characters below U+0300 (the first combining mark) are never surrogates, combining marks, joiners,
/// or any other cluster continuation, so they always form their own single-char cluster. The methods
/// here use that as an O(1) fast path (matching the fast path in <see cref="Rendering.UnicodeWidth"/>)
/// to avoid scanning - important because the caret can be deep into a long document.
/// </para>
/// </summary>
internal static class Grapheme
{
    private const char FirstCombiningMark = '\u0300';

    private static bool IsSimple(char c) => c < FirstCombiningMark;

    /// <summary>
    /// The smallest cluster boundary strictly greater than <paramref name="index"/>
    /// (i.e. the caret position one grapheme to the right), clamped to the text length.
    /// </summary>
    public static int NextBoundary(string text, int index)
    {
        if (index < 0) index = 0;
        if (index >= text.Length) return text.Length;
        // a simple character not followed by a continuation is a single-char cluster
        if (IsSimple(text[index]) && (index + 1 == text.Length || IsSimple(text[index + 1])))
            return index + 1;
        return index + StringInfo.GetNextTextElementLength(text, index);
    }

    /// <summary>
    /// The largest cluster boundary strictly less than <paramref name="index"/>
    /// (i.e. the caret position one grapheme to the left), clamped to 0.
    /// </summary>
    public static int PreviousBoundary(string text, int index)
    {
        if (index <= 0) return 0;
        if (index > text.Length) index = text.Length;
        // if the character ending the previous cluster is simple, that cluster is exactly one char
        if (IsSimple(text[index - 1])) return index - 1;
        return ScanToBoundary(text, index, inclusive: false);
    }

    /// <summary>
    /// The largest cluster boundary less than or equal to <paramref name="index"/>. Snaps an index
    /// that may have landed in the middle of a cluster (e.g. from column-based vertical navigation)
    /// back onto a boundary, without moving it if it is already on one.
    /// </summary>
    public static int RoundDownToBoundary(string text, int index)
    {
        if (index <= 0) return 0;
        if (index >= text.Length) return text.Length;
        // a simple character at 'index' always starts a new cluster, so 'index' is already a boundary
        if (IsSimple(text[index])) return index;
        return ScanToBoundary(text, index, inclusive: true);
    }

    // Walks clusters from the start of the text to find the boundary at (inclusive) or just below
    // (exclusive) 'index'. Only reached for text containing surrogates/combining marks near the caret.
    private static int ScanToBoundary(string text, int index, bool inclusive)
    {
        int i = 0;
        while (i < text.Length)
        {
            int next = i + StringInfo.GetNextTextElementLength(text, i);
            if (inclusive && next == index) return index;
            if (next >= index) return i;
            i = next;
        }
        return i;
    }
}
