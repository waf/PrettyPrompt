#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Globalization;
using PrettyPrompt.Rendering;
using Xunit;

namespace PrettyPrompt.Tests;

public class UnicodeWidthTests
{
    [Theory]
    // basics
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("abc", 3)]
    [InlineData("\u4E66", 2)]            // 书 full-width CJK
    [InlineData("a\u4E66bc", 5)]         // mixed narrow + wide

    // single emoji (supplementary plane)
    [InlineData("\U0001F600", 2)]        // 😀

    // multi-codepoint grapheme clusters from issue #270 — each is a single two-column cell,
    // NOT the sum of its parts (which previously produced widths of 3-5 and crashed).
    [InlineData("\U0001F926\U0001F3FC", 2)]                       // 🤦🏼 base + skin-tone modifier
    [InlineData("\U0001F926\u200D\u2642", 2)]                     // 🤦‍♂ base + ZWJ + male sign
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0F", 2)]     // 🤦🏼‍♂️ full ZWJ sequence
    [InlineData("\u79B0\U000E0100", 2)]                           // 禰󠄀 CJK + variation selector supplement (U+E0100)

    // base char + combining mark is one column (the trailing mark adds nothing)
    [InlineData("e\u0301", 1)]                                    // é = e + combining acute accent

    // multiple clusters sum their (capped) widths
    [InlineData("a\U0001F926\U0001F3FCb", 4)]                     // narrow + two-column cluster + narrow

    // narrow supplementary glyph that the old code mis-sized (issue called this out): a playing card is 1
    [InlineData("\U0001F0A1", 1)]                                 // 🂡 playing card ace of spades

    // U+FE0F (emoji variation selector) promotes a default-text base into a two-column emoji. The bare base
    // is one column, but base + VS16 renders as a 2-column emoji in terminals, so the caret/cursor must size
    // it as two columns to stay aligned past it. See https://github.com/waf/PrettyPrompt/issues/270.
    [InlineData("⚠", 1)]                                     // ⚠ warning sign, text presentation = 1 column
    [InlineData("⚠️", 2)]                               // ⚠️ warning sign + VS16, emoji presentation = 2 columns
    [InlineData("abc⚠️def", 8)]                         // surrounded: abc (3) + ⚠️ (2) + def (3)
    [InlineData("ℹ️", 2)]                               // ℹ️ information source + VS16

    // Halfwidth katakana voiced/semi-voiced sound marks (U+FF9E ﾞ, U+FF9F ﾟ) are spacing grapheme EXTENDERS:
    // StringInfo clusters each with the preceding kana, but - unlike a zero-width combining mark that overlays
    // its base - each renders in its own halfwidth cell, so a kana+mark cluster is two columns. The per-char
    // GetWidth path already counts these (wcwidth gives them 1); the cluster path must match.
    // See https://github.com/microsoft/terminal/issues/18087 and https://github.com/waf/PrettyPrompt/issues/270.
    [InlineData("ﾊﾟｸﾞ", 4)]  // ﾊﾟｸﾞ = パグ ("pug" in halfwidth katakana): 4 columns, not 2
    [InlineData("ﾊﾟ", 2)]              // ﾊﾟ one kana + semi-voiced sound mark (U+FF9F) cluster = 2 columns
    [InlineData("ｸﾞ", 2)]              // ｸﾞ one kana + voiced sound mark (U+FF9E) cluster = 2 columns
    [InlineData("ﾞ", 1)]                    // a lone voiced sound mark is its own one-column cluster
    [InlineData("aﾊﾟb", 4)]            // surrounded: a (1) + ﾊﾟ (2) + b (1)
    public void GetWidth_ReturnsExpectedDisplayWidth(string text, int expectedWidth)
    {
        Assert.Equal(expectedWidth, UnicodeWidth.GetWidth(text));
    }

    [Theory]
    // a single grapheme cluster never exceeds two columns, regardless of how many scalars it contains
    [InlineData("\U0001F926", 2)]                                 // 🤦
    [InlineData("\U0001F926\U0001F3FC", 2)]                       // 🤦🏼
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0F", 2)]     // 🤦🏼‍♂️
    [InlineData("\u79B0\U000E0100", 2)]                           // 禰󠄀
    [InlineData("a", 1)]                                          // narrow
    [InlineData("\u4E66", 2)]                                     // 书 wide
    [InlineData("⚠", 1)]                                        // ⚠ warning sign on its own = text presentation, 1 column
    [InlineData("⚠️", 2)]                                        // ⚠️ warning sign + VS16 = emoji presentation, 2 columns
    // halfwidth kana + halfwidth (semi-)voiced sound mark: the mark is a spacing extender, not a zero-width
    // combining mark, so it adds its own column - see microsoft/terminal#18087.
    [InlineData("ﾊﾟ", 2)]                                       // ﾊﾟ = U+FF8A + semi-voiced sound mark U+FF9F
    [InlineData("ｸﾞ", 2)]                                       // ｸﾞ = U+FF78 + voiced sound mark U+FF9E
    [InlineData("ﾞ", 1)]                                          // a lone halfwidth voiced sound mark = 1 column
    public void GetGraphemeClusterWidth_IsCappedAtTwo(string cluster, int expectedWidth)
    {
        Assert.Equal(expectedWidth, UnicodeWidth.GetGraphemeClusterWidth(cluster));
        Assert.InRange(UnicodeWidth.GetGraphemeClusterWidth(cluster), 0, 2);
    }

    [Theory]
    // GetLengthThatFits returns a CHAR count whose display width fits the column budget, on a cluster boundary.
    [InlineData("abcde", 3, 3)]                          // narrow: 3 columns == 3 chars
    [InlineData("\u4E66\u4E66\u4E66", 4, 2)]                          // each wide char is 2 columns; 4 columns fits 2 chars
    [InlineData("\u4E66\u4E66\u4E66", 5, 2)]                          // 5 columns: a 3rd wide char would be 6, so stop at 2 chars
    [InlineData("a\u4E66", 1, 1)]                            // 'a' fits 1 column; 书 would overflow
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0Fx", 2, 7)]  // the width-2 emoji (7 chars) fits in 2 columns; 'x' would not
    [InlineData("\U0001F926\U0001F3FC\u200D\u2642\uFE0Fx", 1, 0)]  // the emoji is 2 columns wide and cannot fit in 1
    public void GetLengthThatFits_TruncatesByWidthOnClusterBoundary(string text, int maxWidth, int expectedLength)
        => Assert.Equal(expectedLength, UnicodeWidth.GetLengthThatFits(text, maxWidth));

    /// <summary>
    /// One fragment per hazard the ASCII fast path has to respect. Keep these as \uXXXX escapes - written as
    /// literals, the invisible ones get mangled and the test quietly stops testing anything.
    /// </summary>
    private static readonly string[] DifferentialFragments =
    {
        // printable ASCII, including the exact range endpoints
        "", "a", "abc", "hello world", "\u0020", "\u007E",
        // outside the range: controls, tab, DEL, and CR LF (the only all-ASCII cluster)
        "\u0000", "\u0009", "\u007F", "\n", "\r\n", "a\r\nb", "abc\r\ndef",
        // combining mark / ZWJ / VS16 clustering onto the PRECEDING char - the key hazard
        "e\u0301", "ae\u0301b", "ab\u0107", "a\u200Db", "x\uFE0F", "abc\u26A0\uFE0Fdef",
        // wide and supplementary-plane
        "\u4E66", "a\u4E66b", "\U0001F600", "a\U0001F600b",
        "\U0001F926\U0001F3FC\u200D\u2642\uFE0F", "abc\U0001F926\U0001F3FC\u200D\u2642\uFE0Fdef",
        // halfwidth kana + spacing sound mark, and VS16 promotion
        "\uFF8A\uFF9F", "a\uFF8A\uFF9Fb", "\u26A0", "\u26A0\uFE0F",
        "\uD83D", // lone high surrogate (ill-formed)
    };

    public static TheoryData<string> DifferentialCorpus()
    {
        var data = new TheoryData<string>();
        foreach (var first in DifferentialFragments)
        {
            data.Add(first);
            foreach (var second in DifferentialFragments)
            {
                data.Add(first + second);
            }
        }
        return data;
    }

    /// <summary>
    /// Pins the fast path to the general walker. The corpus pairs every hazard fragment with every other, so
    /// each one lands at a run boundary - where "printable ASCII is its own cluster" stops holding.
    /// </summary>
    [Theory]
    [MemberData(nameof(DifferentialCorpus))]
    public void GetWidth_FastPathMatchesGraphemeWalker(string text)
        => Assert.Equal(WidthByWalker(text), UnicodeWidth.GetWidth(text));

    [Theory]
    [MemberData(nameof(DifferentialCorpus))]
    public void GetLengthThatFits_FastPathMatchesGraphemeWalker(string text)
    {
        // every budget from 0 to past the full width, covering both the "runs out mid-run" and
        // "consumes everything" branches.
        for (int maxWidth = 0; maxWidth <= WidthByWalker(text) + 2; maxWidth++)
        {
            Assert.Equal(LengthThatFitsByWalker(text, maxWidth), UnicodeWidth.GetLengthThatFits(text, maxWidth));
        }
    }

    /// <summary>Reference: always walks clusters, never takes the fast path.</summary>
    private static int WidthByWalker(string text)
    {
        int width = 0;
        int i = 0;
        while (i < text.Length)
        {
            int elementLength = StringInfo.GetNextTextElementLength(text, i);
            width += UnicodeWidth.GetGraphemeClusterWidth(text.AsSpan(i, elementLength));
            i += elementLength;
        }
        return width;
    }

    /// <summary>Reference for <see cref="UnicodeWidth.GetLengthThatFits"/>, cluster by cluster.</summary>
    private static int LengthThatFitsByWalker(string text, int maxWidth)
    {
        if (maxWidth <= 0) return 0;
        int width = 0;
        int i = 0;
        while (i < text.Length)
        {
            int elementLength = StringInfo.GetNextTextElementLength(text, i);
            int elementWidth = UnicodeWidth.GetGraphemeClusterWidth(text.AsSpan(i, elementLength));
            if (width + elementWidth > maxWidth) break;
            width += elementWidth;
            i += elementLength;
        }
        return i;
    }
}
