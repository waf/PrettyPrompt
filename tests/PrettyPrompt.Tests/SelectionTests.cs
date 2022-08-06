#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Tests;

public class SelectionTests
{
    [Fact]
    public async Task ReadLine_HorizontalSelectWordAndType_ReplacesWord()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"The quick brown fox jumped over the lazy dog{Control | Shift}{LeftArrow}giraffe",
            $"{Control}{LeftArrow}{Control}{LeftArrow}{Control | Shift}{RightArrow}enigmatic {Enter}");
        var prompt = new Prompt(console: console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("The quick brown fox jumped over the enigmatic giraffe", result.Text);
    }

    [Fact]
    public async Task ReadLine_SelectAllAndType_ReplacesEverything()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"The quick brown fox jumped over the lazy dog{Control}{A}bonk{Enter}");
        var prompt = new Prompt(console: console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("bonk", result.Text);
    }

    [Fact]
    public async Task ReadLine_HorizontalSelectTextWithArrowKeys_SelectsText()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"I am really happy!",
            $"{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}",
            $"{Control | Shift}{RightArrow}so",
            $"{Shift}{LeftArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}",
            $"{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}",
            $"up{Enter}"
            );
        var prompt = new Prompt(console: console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("I am soup!", result.Text);
    }

    [Fact]
    public async Task ReadLine_VerticalSelectTextWithArrowKeys_SelectsText()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"There once was a REPL from Spain{Shift}{Enter}",
            $"Whose text select was a bit of a pain{Shift}{Enter}",
            $"But through unit testing{Shift}{Enter}",
            $"Which was quite interesting{Shift}{Enter}",
            $"The implementation was proved right as rain!",
            $"{UpArrow}{UpArrow}",
            $"{Control}{LeftArrow}{Control}{LeftArrow}{LeftArrow}",
            $"{Shift}{UpArrow}{Shift}{UpArrow}{Shift}{UpArrow}",
            $"{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Delete}{Delete}",
            $"{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{Home}{Shift}{End}{Shift}{DownArrow}{Shift}{DownArrow}",
            $"{Control | Shift}{End}{Control | Shift}{LeftArrow}{Control | Shift}{LeftArrow}{Delete}{Enter}"
            );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("There once was rain!", result.Text);
    }

    [Fact]
    public async Task ReadLine_TextOperationsWithUndo_AreUndone()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"It's a small world, after all",
            $"{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}",
            $"{Control | Shift}{RightArrow}{Delete}",
            $"{Control}{Z}{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("It's a small world, after all", result.Text);
    }

    [Fact]
    public async Task ReadLine_TextOperationsWithRedo_AreRedone()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"It's a small world, after all",
            $"{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}",
            $"{Control | Shift}{RightArrow}{Delete}",
            $"{Control}{Z}",
            $"{Control}{Y}{Control}{Y}{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("It's a world, after all", result.Text);
    }

    [Fact]
    public async Task ReadLine_CopiedText_CanBePasted()
    {
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.StubInput(
                $"baby shark doo",
                $"{Control | Shift}{LeftArrow}{Shift}{LeftArrow}{Control}{C}{End}",
                $"{Control}{V}{Control}{V}{Control}{V}{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal("baby shark doo doo doo doo", result.Text);
        }
    }

    [Fact]
    public async Task ReadLine_CutText_CanBePasted()
    {
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.StubInput(
                $"baby shark doo doo doo doo",
                $"{Home}{Control | Shift}{RightArrow}{Control | Shift}{RightArrow}{Shift}{LeftArrow}",
                $"{Control}{X}{Delete}{End} {Control}{V}{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal("doo doo doo doo baby shark", result.Text);
        }
    }

    [Fact]
    public async Task ReadLine_Delete_LeftSelection()
    {
        //left select all + delete
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput(
            $"abcd",
            $"{Shift}{Home}{Delete}{Enter}"
        );
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);


        //left select 'c' + delete
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{Shift}{LeftArrow}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abd", result.Text);


        //left select 'bc' + delete
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ad", result.Text);


        //left select 'abc' + delete
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{Shift}{Home}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("d", result.Text);
    }

    [Fact]
    public async Task ReadLine_Delete_RightSelection()
    {
        //right select all + delete
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput(
            $"abcd",
            $"{Home}{Shift}{End}{Delete}{Enter}"
        );
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);


        //right select 'b' + delete
        console.StubInput(
            $"abcd",
            $"{Home}{RightArrow}{Shift}{RightArrow}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("acd", result.Text);


        //right select 'bc' + delete
        console.StubInput(
            $"abcd",
            $"{Home}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ad", result.Text);


        //right select 'bcd' + delete
        console.StubInput(
            $"abcd",
            $"{Home}{RightArrow}{Shift}{End}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Text);
    }

    [Fact]
    public async Task ReadLine_Delete_UpSelection()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput(
            $"abcd{Shift}{Enter}",
            $"efgh",
            $"{LeftArrow}{LeftArrow}{Shift}{UpArrow}{Delete}{Enter}"
        );
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abgh", result.Text);
    }

    [Fact]
    public async Task ReadLine_Delete_DownSelection()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput(
            $"abcd{Shift}{Enter}",
            $"efgh",
            $"{Control}{Home}{RightArrow}{RightArrow}{Shift}{DownArrow}{Delete}{Enter}"
        );
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abgh", result.Text);
    }

    [Fact]
    public async Task ReadLine_EmptySelectionAndDelete()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        await Test($"{Control}{A}");
        await Test($"{Shift}{LeftArrow}");
        await Test($"{Shift}{RightArrow}");
        await Test($"{Shift}{UpArrow}");
        await Test($"{Shift}{DownArrow}");
        await Test($"{Control}{Shift}{LeftArrow}");
        await Test($"{Control}{Shift}{RightArrow}");
        await Test($"{Control}{Shift}{UpArrow}");
        await Test($"{Control}{Shift}{DownArrow}");
        await Test($"{Shift}{Home}");
        await Test($"{Shift}{End}");

        async Task Test(FormattableString input)
        {
            console.StubInput(input, $"{Delete}{Enter}");
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("", result.Text);
        }
    }

    [Fact]
    public async Task ReadLine_ClearSelectionWhenBecomesEmpty()
    {
        //select left + right -> empty
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{LeftArrow}",
            $"{Shift}{LeftArrow}{Shift}{RightArrow}X{Enter}"
        );
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abXcd", result.Text);


        //select 2x left +  2x right -> empty
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{LeftArrow}",
            $"{Shift}{LeftArrow}{Shift}{LeftArrow}{Shift}{RightArrow}{Shift}{RightArrow}X{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abXcd", result.Text);


        //select left + right + right -> delete 'c'
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{LeftArrow}",
            $"{Shift}{LeftArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abd", result.Text);


        //select right + right + Home -> delete 'ab'
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{LeftArrow}",
            $"{Shift}{RightArrow}{Shift}{RightArrow}{Shift}{Home}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("cd", result.Text);


        //select right + left + left -> delete 'b'
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{LeftArrow}",
            $"{Shift}{RightArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("acd", result.Text);


        //select left + left + End -> delete 'cd'
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{LeftArrow}",
            $"{Shift}{LeftArrow}{Shift}{LeftArrow}{Shift}{End}{Delete}{Enter}"
        );
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ab", result.Text);
    }

    [Fact]
    public async Task ReadLine_Delete_SmartHomeSelection()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput(
            $"    abcd{Home}{Home}", //get caret at location 0
            $"{Shift}{Home}{Delete}{Enter}" //select first 4 spaces and delete them
        );
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abcd", result.Text);
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/47.
    /// </summary>
    [Fact]
    public async Task ReadLine_Write_SelectedAll_Confirm_ShouldRedraw()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"123{Shift}{Enter}",
            $"456{Shift}{Enter}",
            $"789{Control}{A}", //select all
            $"{Control}{Enter}"); //confirm input

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);

        var output = console.GetAllOutput();
        var indexOfOutputWithSelection = output.Select((o, i) => (o, i)).First(t => t.o.Contains("[39;49;7m")).i; //reset + reverse
        var redraw = output[indexOfOutputWithSelection + 1];
        Assert.Contains("123", redraw);
        Assert.Contains("456", redraw);
        Assert.Contains("789", redraw);
        Assert.DoesNotContain("7m", redraw); //reverse
    }

    [Fact]
    public async Task ReadLine_WriteLetter_LeftSelect_OverWrite_CheckRedraw()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput(
            $"a",
            $"{Shift}{LeftArrow}b",
            $"{Enter}"
        );
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        var outputs = console.GetAllOutput();
        Assert.Equal("a", outputs[2]);
        Assert.Equal("\u001b[1D\u001b[39;49;7ma\u001b[0m\u001b[1D", outputs[3]); //move left, rewrite 'a' with reverse colors, reset, move left
        Assert.Equal("b", outputs[4]);
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/156.
    /// </summary>
    [Fact]
    public async Task ReadLine_SelectedAll_SomeKeysShouldNotDeselectText()
    {
        foreach (var key in new[] { LeftWindows, RightWindows, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 })
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"abcd",
                $"{Control}{A}",
                $"{key}",
                $"x",
                $"{Enter}"); //confirm input

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("x", result.Text);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/173
    /// </summary>
    [Fact]
    public async Task ReadLine_ReplaceSelectionByText()
    {
        //replace by Tab
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput($"abcdefg{Control}{A}{Tab}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal(PromptTests.DefaultTabSpaces, result.Text);

        //replace by Paste (shorter text)
        console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText("ab");
            prompt = new Prompt(console: console);
            console.StubInput($"abcdefg{Control}{A}{Control}{V}{Enter}");
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("ab", result.Text);
        }

        //replace by Paste (longer text)
        console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText("1234567890");
            prompt = new Prompt(console: console);
            console.StubInput($"abcdefg{Control}{A}{Control}{V}{Enter}");
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("1234567890", result.Text);
        }
    }


    #region Multiline tab indentation/dedentation (https://github.com/waf/PrettyPrompt/issues/174)
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadLine_MultilineSelectionIndentation_SelectionFromLeftToRight(bool selectionFromLeftToRight)
    {
        var text = "abc\ncd\nefgh";
        for (int selectionStart = 0; selectionStart <= 5; selectionStart++)
            for (int selectionLength = 1; selectionLength <= 6 - selectionStart; selectionLength++)
            {
                if (!text.AsSpan(selectionStart, selectionLength).Contains('\n')) continue;

                var console = ConsoleStub.NewConsole();
                var prompt = new Prompt(console: console);

                var input = new List<FormattableString>();
                foreach (var item in text.Split('\n'))
                {
                    input.Add($"{item}");
                    input.Add($"{Shift}{Enter}");
                }
                input.RemoveAt(input.Count - 1);
                MultilineSelection_GenerateSelection(selectionStart, selectionLength, input, selectionFromLeftToRight);
                input.Add($"{Tab}{Enter}");
                console.StubInput(input.ToArray());

                var result = await prompt.ReadLineAsync();
                Assert.True(result.IsSuccess);

                var expectedResult =
                    text[selectionStart + selectionLength - 1] == '\n' ?
                    $"{PromptTests.DefaultTabSpaces}abc\ncd\nefgh" :
                    $"{PromptTests.DefaultTabSpaces}abc\n{PromptTests.DefaultTabSpaces}cd\nefgh";
                Assert.Equal(expectedResult, result.Text.Replace("\r", ""));
            }

        for (int selectionStart = 4; selectionStart < text.Length - 1; selectionStart++)
            for (int selectionLength = 1; selectionLength <= text.Length - selectionStart; selectionLength++)
            {
                if (!text.AsSpan(selectionStart, selectionLength).Contains('\n')) continue;

                var console = ConsoleStub.NewConsole();
                var prompt = new Prompt(console: console);

                var input = new List<FormattableString>();
                foreach (var item in text.Split('\n'))
                {
                    input.Add($"{item}");
                    input.Add($"{Shift}{Enter}");
                }
                input.RemoveAt(input.Count - 1);
                MultilineSelection_GenerateSelection(selectionStart, selectionLength, input, selectionFromLeftToRight);
                input.Add($"{Tab}{Enter}");
                console.StubInput(input.ToArray());

                var result = await prompt.ReadLineAsync();
                Assert.True(result.IsSuccess);

                var expectedResult =
                    text[selectionStart + selectionLength - 1] == '\n' ?
                    $"abc\n{PromptTests.DefaultTabSpaces}cd\nefgh" :
                    $"abc\n{PromptTests.DefaultTabSpaces}cd\n{PromptTests.DefaultTabSpaces}efgh";
                Assert.Equal(expectedResult, result.Text.Replace("\r", ""));
            }

        //all lines
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            var input = new List<FormattableString>();
            foreach (var item in text.Split('\n'))
            {
                input.Add($"{item}");
                input.Add($"{Shift}{Enter}");
            }
            input.RemoveAt(input.Count - 1);
            MultilineSelection_GenerateSelectAll(input, selectionFromLeftToRight);
            input.Add($"{Tab}{Enter}");
            console.StubInput(input.ToArray());

            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal($"{PromptTests.DefaultTabSpaces}abc\n{PromptTests.DefaultTabSpaces}cd\n{PromptTests.DefaultTabSpaces}efgh", result.Text.Replace("\r", ""));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadLine_MultilineSelectionDedentation_SelectionFromLeftToRight(bool selectionFromLeftToRight)
    {
        var T = PromptTests.DefaultTabSpaces;
        var TL = PromptTests.DefaultTabSpaces.Length;
        var text = $"{T}abc\n{T}cd\n{T}efgh";
        for (int selectionStart = 0; selectionStart <= TL + 5; selectionStart++)
            for (int selectionLength = 1; selectionLength <= 2 * TL + 6 - selectionStart; selectionLength++)
            {
                if (!text.AsSpan(selectionStart, selectionLength).Contains('\n')) continue;

                var console = ConsoleStub.NewConsole();
                var prompt = new Prompt(console: console);

                var input = new List<FormattableString>();
                foreach (var item in text.Split('\n'))
                {
                    input.Add($"{item}");
                    input.Add($"{Shift}{Enter}");
                }
                input.RemoveAt(input.Count - 1);
                MultilineSelection_GenerateSelection(selectionStart, selectionLength, input, selectionFromLeftToRight);
                input.Add($"{Shift}{Tab}{Enter}");
                console.StubInput(input.ToArray());

                var result = await prompt.ReadLineAsync();
                Assert.True(result.IsSuccess);

                var expectedResult =
                    text[selectionStart + selectionLength - 1] == '\n' ?
                    $"abc\n{T}cd\n{T}efgh" :
                    $"abc\ncd\n{T}efgh";
                Assert.Equal(expectedResult, result.Text.Replace("\r", ""));
            }

        for (int selectionStart = TL + 4; selectionStart < text.Length - 1; selectionStart++)
            for (int selectionLength = 1; selectionLength <= text.Length - selectionStart; selectionLength++)
            {
                if (!text.AsSpan(selectionStart, selectionLength).Contains('\n')) continue;

                var console = ConsoleStub.NewConsole();
                var prompt = new Prompt(console: console);

                var input = new List<FormattableString>();
                foreach (var item in text.Split('\n'))
                {
                    input.Add($"{item}");
                    input.Add($"{Shift}{Enter}");
                }
                input.RemoveAt(input.Count - 1);
                MultilineSelection_GenerateSelection(selectionStart, selectionLength, input, selectionFromLeftToRight);
                input.Add($"{Shift}{Tab}{Enter}");
                console.StubInput(input.ToArray());

                var result = await prompt.ReadLineAsync();
                Assert.True(result.IsSuccess);

                var expectedResult =
                    text[selectionStart + selectionLength - 1] == '\n' ?
                    $"{T}abc\ncd\n{T}efgh" :
                    $"{T}abc\ncd\nefgh";
                Assert.Equal(expectedResult, result.Text.Replace("\r", ""));
            }

        //all lines
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            var input = new List<FormattableString>();
            foreach (var item in text.Split('\n'))
            {
                input.Add($"{item}");
                input.Add($"{Shift}{Enter}");
            }
            input.RemoveAt(input.Count - 1);
            MultilineSelection_GenerateSelectAll(input, selectionFromLeftToRight);
            input.Add($"{Shift}{Tab}{Enter}");
            console.StubInput(input.ToArray());

            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal($"abc\ncd\nefgh", result.Text.Replace("\r", ""));
        }
    }

    private static void MultilineSelection_GenerateSelection(int selectionStart, int selectionLength, List<FormattableString> input, bool selectionFromLeftToRight)
    {
        input.Add($"{Control}{Home}");
        if (selectionFromLeftToRight)
        {
            for (int k = 0; k < selectionStart; k++) input.Add($"{RightArrow}");
            for (int k = 0; k < selectionLength; k++) input.Add($"{Shift}{RightArrow}");
        }
        else
        {
            for (int k = 0; k < selectionStart + selectionLength; k++) input.Add($"{RightArrow}");
            for (int k = 0; k < selectionLength; k++) input.Add($"{Shift}{LeftArrow}");
        }
    }

    private static void MultilineSelection_GenerateSelectAll(List<FormattableString> input, bool selectionFromLeftToRight)
    {
        if (selectionFromLeftToRight)
        {
            input.Add($"{Control}{A}");
        }
        else
        {
            input.Add($"{Control}{End}{Control}{Shift}{Home}");
        }
    }
    #endregion
}