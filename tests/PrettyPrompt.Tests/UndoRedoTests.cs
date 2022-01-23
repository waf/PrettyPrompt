#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;

namespace PrettyPrompt.Tests;

public class UndoRedoTests
{
    [Fact]
    public async Task ReadLine_ImmediateRedo()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"{Control}{Y}{Control}{Y}{Control}{Y}", //should do nothing
            $"{Control}{Z}{Control}{Z}{Control}{Z}", //should do nothing
            $"{Control}{Y}{Control}{Y}{Control}{Y}", //should do nothing
            $"{Control}{Z}{Control}{Y}", //should do nothing
            $"{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public async Task ReadLine_SimpleUndoRedo()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"a",
            $"{Control}{Z}", //undo
            $"{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"a",
            $"{Control}{Z}", //undo
            $"{Control}{Y}", //redo
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Text);

        var outputs = console.GetAllOutput();
        Assert.Equal("a", outputs[1]);
        Assert.Equal($"{MoveCursorLeft(1)} {MoveCursorLeft(1)}", outputs[2]); //delete of 'a'
        Assert.Equal("a", outputs[3]);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"a",
            $"{Control}{Z}", //undo
            $"{Control}{Z}{Control}{Z}{Control}{Z}", //should do nothing
            $"{Control}{Y}", //redo
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Text);


        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"a",
            $"{Control}{Z}", //undo
            $"{Control}{Z}{Control}{Z}{Control}{Z}", //should do nothing
            $"{Control}{Y}", //redo
            $"{Control}{Y}{Control}{Y}{Control}{Y}", //should do nothing
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Text);
    }

    [Fact]
    public async Task ReadLine_SimpleUndoRedoSequence()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{Control}{Z}", //undo a
            $"{Control}{Z}", //undo b
            $"{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ab", result.Text);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{Control}{Z}", //undo a
            $"{Control}{Z}", //undo b
            $"{Control}{Z}", //undo c
            $"{Control}{Z}", //undo d
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{Control}{Z}", //undo a
            $"{Control}{Z}", //undo b
            $"{Control}{Z}", //undo c
            $"{Control}{Z}", //undo d
            $"{Control}{Y}", //redo a
            $"{Control}{Y}", //redo b
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ab", result.Text);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{Control}{Z}", //undo a
            $"{Control}{Z}", //undo b
            $"{Control}{Z}", //undo c
            $"{Control}{Z}", //undo d
            $"{Control}{Y}", //redo a
            $"{Control}{Y}", //redo b
            $"{Control}{Y}", //redo c
            $"{Control}{Y}", //redo d
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abcd", result.Text);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{Control}{Z}", //undo a
            $"{Control}{Z}", //undo b
            $"{Control}{Z}", //undo c
            $"{Control}{Z}", //undo d
            $"{Control}{Z}{Control}{Z}{Control}{Z}", //should do nothing
            $"{Control}{Y}", //redo a
            $"{Control}{Y}", //redo b
            $"{Control}{Y}", //redo c
            $"{Control}{Y}", //redo d
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abcd", result.Text);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{Control}{Z}", //undo a
            $"{Control}{Z}", //undo b
            $"{Control}{Z}", //undo c
            $"{Control}{Z}", //undo d
            $"{Control}{Z}{Control}{Z}{Control}{Z}", //should do nothing
            $"{Control}{Y}", //redo a
            $"{Control}{Y}", //redo b
            $"{Control}{Y}", //redo c
            $"{Control}{Y}", //redo d
            $"{Control}{Y}{Control}{Y}{Control}{Y}", //should do nothing
            $"{Enter}"
        );
        prompt = new Prompt(console: console);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abcd", result.Text);
    }

    //Reproduces bug from https://github.com/waf/PrettyPrompt/issues/76
    [Fact]
    public async Task ReadLine_Letter_Undo_Letter_Undo_ResultShouldBeEmpty()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"a",
            $"{Control}{Z}", //undo
            $"b",
            $"{Control}{Z}", //redo
            $"{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);

        var outputs = console.GetAllOutput();
        Assert.Equal("a", outputs[1]);
        Assert.Equal($"{MoveCursorLeft(1)} {MoveCursorLeft(1)}", outputs[2]); //delete of 'a'
        Assert.Equal("b", outputs[3]);
        Assert.Equal($"{MoveCursorLeft(1)} {MoveCursorLeft(1)}", outputs[4]); //delete of 'b'
    }

    //Reproduces bug from https://github.com/waf/PrettyPrompt/issues/77
    [Fact]
    public async Task ReadLine_ReplaceSelectedText_Undo()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}", //select 'bc'
            $"x", //replace 'bc' with 'x'
            $"{Control}{Z}", //redo
            $"{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abcd", result.Text);

        //---------------------------------------------

        console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText("xyz");
            console.StubInput(
                $"abcd",
                $"{LeftArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}", //select 'bc'
                $"{Control}{V}", //paste 'xyz' (=replace 'bc' with 'xyz')
                $"{Control}{Z}", //redo
                $"{Enter}"
            );
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("abcd", result.Text);
        }
    }
}