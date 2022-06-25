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
        Assert.Equal("a", outputs[2]);
        Assert.Equal($"{GetMoveCursorLeft(1)} {GetMoveCursorLeft(1)}", outputs[3]); //delete of 'a'
        Assert.Equal("a", outputs[4]);

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

    /// <summary>
    /// Reproduces bug from https://github.com/waf/PrettyPrompt/issues/76
    /// </summary>
    [Fact]
    public async Task ReadLine_Letter_Undo_Letter_Undo_ResultShouldBeEmpty()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"a",
            $"{Control}{Z}", //undo
            $"b",
            $"{Control}{Z}", //undo
            $"{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Text);

        var outputs = console.GetAllOutput();
        Assert.Equal("a", outputs[2]);
        Assert.Equal($"{GetMoveCursorLeft(1)} {GetMoveCursorLeft(1)}", outputs[3]); //delete of 'a'
        Assert.Equal("b", outputs[4]);
        Assert.Equal($"{GetMoveCursorLeft(1)} {GetMoveCursorLeft(1)}", outputs[5]); //delete of 'b'
    }

    /// <summary>
    /// Reproduces bug from https://github.com/waf/PrettyPrompt/issues/77
    /// </summary>
    [Fact]
    public async Task ReadLine_ReplaceSelectedText_Undo()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{LeftArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}", //select 'bc'
            $"x", //replace 'bc' with 'x'
            $"{Control}{Z}", //undo
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
                $"{Control}{Z}", //undo
                $"{Enter}"
            );
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("abcd", result.Text);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/116
    /// </summary>
    [Fact]
    public async Task ReadLine_UndoRedoAndCaretPosition()
    {
        for (int i = 1; i <= 4; i++)
        {
            var console = ConsoleStub.NewConsole();
            var inputs = new List<FormattableString>();
            inputs.Add($"abcd");
            inputs.AddRange(Enumerable.Repeat<FormattableString>($"{Control}{Z}", i));
            inputs.Add($"|");
            inputs.Add($"{Enter}");
            console.StubInput(inputs.ToArray());
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("abcd"[..(4 - i)] + "|", result.Text);
        }

        /////////////////////////////////////////////////////////////

        for (int i = 1; i <= 4; i++)
        {
            var console = ConsoleStub.NewConsole();
            var inputs = new List<FormattableString>();
            inputs.Add($"abcd");
            inputs.AddRange(Enumerable.Repeat<FormattableString>($"{LeftArrow}", i));
            inputs.Add($"x");
            inputs.Add($"{Control}{Z}");
            inputs.Add($"|");
            inputs.Add($"{Enter}");
            console.StubInput(inputs.ToArray());
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("abcd".Insert(4 - i, "|"), result.Text);
        }

        /////////////////////////////////////////////////////////////

        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"abcd",
                $"{LeftArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}", //select 'bc'
                $"{Delete}", //delete 'bc'
                $"{End}x", //write 'x' at the end
                $"{Control}{Z}", //undo 'x'
                $"{Control}{Z}", //undo 'bc' delete - it should be selected again
                $"|", //replace 'bc' with caret
                $"{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("a|d", result.Text);
        }

        /////////////////////////////////////////////////////////////

        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"abcd",
                $"{LeftArrow}{Shift}{LeftArrow}{Shift}{LeftArrow}", //select 'bc'
                $"{Delete}", //delete 'bc'
                $"{End}x", //write 'x' at the end
                $"{Control}{Z}", //undo 'x'
                $"{Control}{Z}", //undo 'bc' delete - it should be selected again

                $"{Control}{Z}",
                $"{Control}{Z}",
                $"{Control}{Y}",
                $"{Control}{Y}",

                $"|", //replace 'bc' with caret
                $"{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("a|d", result.Text);
        }
    }

    /// <summary>
    /// Reproduces bug from https://github.com/waf/PrettyPrompt/issues/133
    /// </summary>
    [Fact]
    public async Task ReadLine_Repeated_CtrlZ_Backspace()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abcd",
            $"{Shift}{LeftArrow}{Shift}{LeftArrow}", //select 'cd'
            $"{Backspace}",
            $"{Control}{Z}",
            $"{Backspace}",
            $"{Control}{Z}",
            $"{Enter}"
        );
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abcd", result.Text);
    }

    /// <summary>
    /// Reproduces bug from https://github.com/waf/PrettyPrompt/issues/148
    /// </summary>
    [Fact]
    public async Task ReadLine_CtrlA_CtrlX()
    {
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.StubInput(
                $"abcd",
                $"{Control}{A}",
                $"{Control}{X}",
                $"{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("", result.Text);
        }
    }
}