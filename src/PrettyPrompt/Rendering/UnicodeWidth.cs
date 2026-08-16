#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Buffers;
using System.Globalization;
using Wcwidth;

namespace PrettyPrompt.Rendering;

/// <summary>
/// Calculates how many terminal columns ("cells") a character or string occupies.
///
/// Per-scalar widths come from <see cref="UnicodeCalculator"/> — a vendored, source-only copy of
/// https://github.com/spectreconsole/wcwidth (Unicode 16). On top of that PrettyPrompt adds grapheme-cluster
/// awareness, so a multi-scalar cluster (an emoji ZWJ sequence such as "🤦🏼‍♂️", or a base character followed
/// by combining marks / a variation selector) is sized as the single glyph the terminal draws. The rules:
/// 
/// - A cluster occupies the width of its <b>base scalar value</b>. Trailing combining marks, zero-width joiners,
///   emoji modifiers (e.g. skin tones), and variation selectors shape the glyph but add no columns.
/// - Exception: a halfwidth katakana voiced / semi-voiced sound mark (U+FF9E, U+FF9F) is a grapheme extender
///   too, but it is a <b>spacing</b> mark that takes its own halfwidth cell rather than overlaying the base,
///   so it adds a column — e.g. "ﾊﾟ" is one cluster but two columns.
/// - Every cluster's width is <b>capped at 2</b>, because the renderer models each cell as one or two columns
///   wide (see <see cref="Cell"/>). Without the cap, summing a cluster's parts (e.g. base + skin-tone modifier
///    = 2 + 2) produced widths of 3-5 and crashed cursor positioning.
/// </summary>
/// <remarks>See https://github.com/waf/PrettyPrompt/issues/270</remarks>
public static class UnicodeWidth
{
    /// <summary>
    /// Width of a single UTF-16 code unit. Used by the caret/word-wrap paths that walk text by
    /// <see cref="char"/> (i.e. by string index), so each half of a surrogate pair counts as one column.
    /// For whole strings or grapheme clusters use <see cref="GetWidth(ReadOnlySpan{char})"/> or
    /// <see cref="GetGraphemeClusterWidth(ReadOnlySpan{char})"/> instead.
    /// </summary>
    public static int GetWidth(char character)
        => character < AsciiWidths ? asciiWidths[character] : GetWidthCore(character);

    /// <summary>
    /// Pure memo of <see cref="GetWidthCore"/> over the ASCII range. <c>UnicodeCalculator.GetWidth</c> costs a
    /// <see cref="HashSet{T}"/> probe plus two dictionary lookups and table searches, and word wrap calls it
    /// per character of the document per keystroke: memoizing took a 1000-line re-wrap from ~11.8ms to ~0.5ms.
    /// </summary>
    private const int AsciiWidths = 128;
    private static readonly byte[] asciiWidths = CreateAsciiWidths();

    private static byte[] CreateAsciiWidths()
    {
        var widths = new byte[AsciiWidths];
        for (int i = 0; i < widths.Length; i++)
        {
            // GetWidthCore, not GetWidth - the cache it reads is what we're building.
            widths[i] = (byte)GetWidthCore((char)i);
        }
        return widths;
    }

    private static int GetWidthCore(char character)
    {
        if (character == '\n') return 1; // PrettyPrompt: treat newline as occupying a single column.
        if (char.IsSurrogate(character)) return 1; // half of a surrogate pair; the pair sums to the scalar's width.
        // U+FE0F (emoji variation selector) carries the column its base gains from emoji presentation - e.g.
        // ⚠ (1 col) + VS16 = a 2-col emoji - keeping this per-char sum equal to GetGraphemeClusterWidth.
        if (character == (char)0xFE0F) return 1;
        return Clamp(UnicodeCalculator.GetWidth(character));
    }

    /// <summary>
    /// Total display width of <paramref name="text"/>, summed over its grapheme clusters so the result
    /// matches the number of cells the renderer produces for it.
    /// </summary>
    public static int GetWidth(ReadOnlySpan<char> text)
    {
        int width = 0;
        while (!text.IsEmpty)
        {
            int runLength = LeadingSimpleAsciiRunLength(text);
            if (runLength > 0)
            {
                width += runLength; // one char == one cluster == one column across the run
                text = text.Slice(runLength);
                continue;
            }

            int elementLength = StringInfo.GetNextTextElementLength(text);
            width += GetGraphemeClusterWidth(text.Slice(0, elementLength));
            text = text.Slice(elementLength);
        }
        return width;
    }

    /// <summary>
    /// Length of the leading run of printable ASCII, each char of which is its own one-column cluster, so
    /// callers can account for the run at once instead of walking it. 0 means "no fast run, use the walker".
    ///
    /// <para>
    /// Sound because nothing in [U+0020, U+007E] is a grapheme extender, and the only all-ASCII multi-char
    /// cluster is CR LF ('\r' is below the range). What a run can't rule out is what FOLLOWS it - a combining
    /// mark, ZWJ or VS16 there clusters onto the run's last char - so a run ending inside the text gives that
    /// char back to the walker. The search itself is vectorized.
    /// </para>
    /// </summary>
    private static int LeadingSimpleAsciiRunLength(ReadOnlySpan<char> text)
    {
        int firstSpecial = text.IndexOfAnyExceptInRange(' ', '~');
        return firstSpecial < 0
            ? text.Length        // all printable ASCII; nothing follows to cluster onto it
            : firstSpecial - 1;  // reserve the boundary char (0 or -1 means "no fast run")
    }

    /// <summary>
    /// Display width (0, 1, or 2 columns) of a single grapheme cluster, determined by its base scalar
    /// value. Trailing combining marks, zero-width joiners, emoji modifiers, and variation selectors are
    /// part of the same cluster and contribute no additional columns. Capped at 2 to match the cell model.
    /// </summary>
    public static int GetGraphemeClusterWidth(ReadOnlySpan<char> cluster)
    {
        if (cluster.IsEmpty) return 0;
        if (Rune.DecodeFromUtf16(cluster, out var baseRune, out int baseLength) != OperationStatus.Done)
        {
            return 1; // ill-formed (e.g. a lone surrogate); be defensive and reserve a single column.
        }
        if (baseRune.Value == '\n') return 1;
        // U+FE0F (emoji variation selector) forces emoji presentation, which is 2 columns even for a base
        // that defaults to 1 - e.g. ⚠ (U+26A0) is 1 column but ⚠️ is a 2-column emoji; wcwidth misses this.
        // The length check skips a lone, base-less selector (no width of its own).
        if (cluster.Length > 1 && cluster.Contains((char)0xFE0F)) return 2;

        int width = Clamp(UnicodeCalculator.GetWidth(baseRune));

        // Halfwidth katakana voiced / semi-voiced sound marks (U+FF9E ﾞ, U+FF9F ﾟ) are SPACING grapheme
        // extenders: StringInfo clusters each onto the preceding kana, but unlike a combining mark that
        // overlays its base they render in their own halfwidth cell (category Lm, wcwidth 1). So e.g. "ﾊﾟ"
        // (U+FF8A U+FF9F) is one cluster but occupies two columns. Add a column per trailing mark, which also
        // keeps this in step with the per-char GetWidth path (it already counts them). The base path above
        // handles a lone, base-less mark. See https://github.com/microsoft/terminal/issues/18087.
        foreach (var c in cluster.Slice(baseLength))
        {
            if (c is (char)0xFF9E or (char)0xFF9F) width++;
        }

        return Math.Min(width, 2); // cap at the cell model's two columns (see remarks)
    }

    /// <summary>
    /// Returns the number of leading <see cref="char"/>s of <paramref name="text"/> whose combined
    /// display width does not exceed <paramref name="maxWidth"/> columns. The returned length always
    /// falls on a grapheme-cluster boundary, so slicing the text at it never splits a cluster or a
    /// surrogate pair. Use this for width-bounded truncation instead of slicing by raw character count.
    /// </summary>
    public static int GetLengthThatFits(ReadOnlySpan<char> text, int maxWidth)
    {
        if (maxWidth <= 0) return 0;
        int width = 0;
        int i = 0;
        while (i < text.Length)
        {
            int runLength = LeadingSimpleAsciiRunLength(text.Slice(i));
            if (runLength > 0)
            {
                // budget maps straight onto a char count here, and every offset in the run is a cluster
                // boundary, so clipping mid-run is safe.
                int take = Math.Min(runLength, maxWidth - width);
                width += take;
                i += take;
                if (take < runLength) break; // ran out of budget inside the run
                continue;
            }

            int elementLength = StringInfo.GetNextTextElementLength(text.Slice(i));
            int elementWidth = GetGraphemeClusterWidth(text.Slice(i, elementLength));
            if (width + elementWidth > maxWidth) break;
            width += elementWidth;
            i += elementLength;
        }
        return i;
    }

    // wcwidth returns -1 for control characters; PrettyPrompt renders those as zero width. The renderer
    // models a cell as at most two columns, so never let a single element exceed that.
    private static int Clamp(int wcwidth) => wcwidth < 0 ? 0 : Math.Min(wcwidth, 2);
}
