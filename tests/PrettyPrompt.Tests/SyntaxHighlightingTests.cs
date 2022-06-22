#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Threading.Tasks;
using PrettyPrompt.Highlighting;
using Xunit;
using static System.ConsoleKey;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static PrettyPrompt.Highlighting.AnsiColor;

namespace PrettyPrompt.Tests;

public class SyntaxHighlightingTests
{
    [Fact]
    public async Task ReadLine_SyntaxHighlight()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"red green nocolor blue{Enter}");

        var prompt = new Prompt(
            callbacks: new TestPromptCallbacks
            {
                HighlightCallback = new SyntaxHighlighterTestData().HighlightHandlerAsync
            },
            console: console
        );

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("red green nocolor blue", result.Text);
        var output = console.GetAllOutput();

        // although the words are typed character-by-character, we should still "go back" and redraw
        // it once we know the word should be drawn in a syntax-highlighted color.
        Assert.Contains(
            MoveCursorLeft("red".Length - 1) + ToAnsiEscapeSequenceSlow(SyntaxHighlighterTestData.RedFormat) + "red" + Reset, // when we press 'd' go back two chars and to rewrite the word "red"
            output
        );
        Assert.Contains(
            MoveCursorLeft("green".Length - 1) + ToAnsiEscapeSequenceSlow(SyntaxHighlighterTestData.GreenFormat) + "green" + Reset,
            output
        );
        Assert.Contains(
            MoveCursorLeft("blue".Length - 1) + ToAnsiEscapeSequenceSlow(SyntaxHighlighterTestData.BlueFormat) + "blue" + Reset,
            output
        );

        Assert.DoesNotContain("nocolor", output); // it should output character by character as we type; never the whole string at once.
    }

    [Fact]
    public async Task ReadLine_CJKCharacters_SyntaxHighlight()
    {
        var format1 = new ConsoleFormat(Foreground: Red);
        var format2 = new ConsoleFormat(Foreground: Blue);
        var format3 = new ConsoleFormat(Foreground: Green);

        var console = ConsoleStub.NewConsole(width: 20);
        console.StubInput($"苹果 o 蓝莓 o avocado o{Enter}");

        var prompt = new Prompt(
            callbacks: new TestPromptCallbacks
            {
                HighlightCallback = new SyntaxHighlighterTestData(new Dictionary<string, AnsiColor>
                {
                    { "苹果", format1.Foreground!.Value },
                    { "蓝莓", format2.Foreground!.Value },
                    { "avocado", format3.Foreground!.Value }
                }).HighlightHandlerAsync
            },
            console: console
        );

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("苹果 o 蓝莓 o avocado o", result.Text);
        var output = console.GetAllOutput();

        // although the words are typed character-by-character, we should still "go back" and redraw
        // it once we know the word should be drawn in a syntax-highlighted color.
        Assert.Contains(
            MoveCursorLeft(2) + ToAnsiEscapeSequenceSlow(format1) + "苹果" + Reset,
            output
        );

        Assert.Contains(
            MoveCursorLeft(2) + ToAnsiEscapeSequenceSlow(format2) + "蓝莓" + Reset,
            output
        );

        // avocado is green, but wrapped because the console width is narrow.
        Assert.Contains(
            output,
            str => str.Contains(ToAnsiEscapeSequenceSlow(format3) + "avoc\n")
        );

        Assert.Contains(
            output,
            str => str.Contains("ado" + Reset)
        );
    }
}
