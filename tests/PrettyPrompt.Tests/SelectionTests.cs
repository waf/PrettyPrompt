#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
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
        console.StubInput(
            $"baby shark doo",
            $"{Control | Shift}{LeftArrow}{Shift}{LeftArrow}{Control}{C}{End}",
            $"{Control}{V}{Control}{V}{Control}{V}{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("baby shark doo doo doo doo", result.Text);
    }

    [Fact]
    public async Task ReadLine_CutText_CanBePasted()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"baby shark doo doo doo doo",
            $"{Home}{Control | Shift}{RightArrow}{Control | Shift}{RightArrow}{Shift}{LeftArrow}",
            $"{Control}{X}{Delete}{End} {Control}{V}{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("doo doo doo doo baby shark", result.Text);
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
}
