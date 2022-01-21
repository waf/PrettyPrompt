#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static System.Environment;

namespace PrettyPrompt.Tests;

public class CompletionTests
{
    [Fact]
    public async Task ReadLine_SingleCompletion()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Aa{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Aardvark", result.Text);
    }

    [Fact]
    public async Task ReadLine_MultipleCompletion()
    {
        var console = ConsoleStub.NewConsole();
        // complete 3 animals. For the third animal, start completing Alligator, but then backspace, navigate the completion menu and complete as Alpaca instead.
        console.StubInput($"Aa{Enter} Z{Tab} Alli{Backspace}{Backspace}{DownArrow}{UpArrow}{DownArrow}{DownArrow}{RightArrow}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Aardvark Zebra Alpaca", result.Text);
    }

    [Fact]
    public async Task ReadLine_MultilineCompletion()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Aa{Enter}{Shift}{Enter}Z{Control}{Spacebar}{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark{NewLine}Zebra", result.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionMenu_AutoOpens()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"A{Enter}{Shift}{Enter}Z{Enter}{Enter}");

        Prompt prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark{NewLine}Zebra", result.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionMenu_Closes()
    {
        var console = ConsoleStub.NewConsole();
        Prompt prompt = ConfigurePrompt(console);

        // Escape should close menu
        console.StubInput($"A{Escape}{Enter}"); // it will auto-open when we press A (see previous test)
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"A", result.Text);

        // Home key (among others) should close menu
        console.StubInput($"A{Home}{Enter}");
        var result2 = await prompt.ReadLineAsync();

        Assert.True(result2.IsSuccess);
        Assert.Equal($"A", result2.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionMenu_Scrolls()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"{Control}{Spacebar}{Control}{Spacebar}",
            $"{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}",
            $"{Enter}{Enter}"
        );
        Prompt prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Zebra", result.Text);

        console.StubInput(
            $"{Control}{Spacebar}{Control}{Spacebar}",
            $"{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}",
            $"{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}",
            $"{Enter}{Enter}"
        );

        var result2 = await prompt.ReadLineAsync();
        Assert.True(result2.IsSuccess);
        Assert.Equal($"Aardvark", result2.Text);
    }

    [Fact]
    public async Task ReadLine_FullyTypeCompletion_CanOpenAgain()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Aardvark {Control}{Spacebar}{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark Aardvark", result.Text);
    }

    [Fact]
    public async Task ReadLine_EmptyPrompt_AutoOpens()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"{Control}{Spacebar}{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark", result.Text);
    }

    [Fact]
    public async Task ReadLine_OpenWindowAtBeginningOfPrompt_Opens()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"a{LeftArrow} {LeftArrow}a{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark a", result.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionWithNoMatches_DoesNotAutoComplete()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"A{Enter} Q{Enter}"); // first {Enter} selects an autocompletion, second {Enter} submits because there are no completions.

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark Q", result.Text);
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/22.
    /// </summary>
    [Fact]
    public async Task ReadLine_AddNewLines_ReturnToStart_ShowCompletionList_ConfirmInput_ShouldRedraw()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"{Shift}{Enter}{Shift}{Enter}{Shift}{Enter}{Shift}{Enter}", //new lines
            $"{Control}{Home}", //return to start
            $"{Control}{Spacebar}", //show completion list
            $"{Enter}"); //confirm input

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);

        var output = console.GetAllOutput();
        var indexOfOutputWithCompletionPane = output.Select((o, i) => (o, i)).First(t => t.o.Contains("┌───────────")).i;
        Assert.Contains("            ", output[indexOfOutputWithCompletionPane + 1]); //completion pane should be cleared
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/65.
    /// </summary>
    [Fact]
    public async Task ReadLine_CompletionInsertionNotFromEndOfWord()
    {
        const string Text = "abcde";
        for (int insertCaretPosition = 0; insertCaretPosition < Text.Length; insertCaretPosition++)
        {
            var console = ConsoleStub.NewConsole();

            var input = new List<FormattableString>() { $"{Text}" };
            input.AddRange(Enumerable.Repeat<FormattableString>($"{LeftArrow}", count: Text.Length - insertCaretPosition));
            input.Add($"{Enter}{Enter}"); //invoke completion list
            input.Add($"{Enter}{Enter}"); //insert completion

            console.StubInput(input.ToArray());
            var prompt = ConfigurePrompt(console, completions: new[] { Text });
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(Text, result.Text);
        }
    }

    public static Prompt ConfigurePrompt(IConsole console, PromptConfiguration? configuration = null, string[]? completions = null) =>
        new(
            callbacks: new PromptCallbacks
            {
                CompletionCallback = new CompletionTestData(completions).CompletionHandlerAsync
            },
            console: console,
            configuration: configuration
        );
}
