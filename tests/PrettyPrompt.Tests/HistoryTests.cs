#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Tests;

public class HistoryTests
{
    [Fact]
    public async Task ReadLine_WithHistory_DoesNothing()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"{UpArrow}{UpArrow}{DownArrow}{DownArrow}yo world{Enter}");
        var result = await prompt.ReadLineAsync();

        // no exceptions, even though we cycled through history when there was no history to cycle through
        Assert.Equal("yo world", result.Text);
    }

    [Fact]
    public async Task ReadLine_WithHistory_CyclesThroughHistory()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"Hello World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"Howdy World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"How ya' doin world{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{UpArrow}{UpArrow}{UpArrow}{DownArrow}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal("Howdy World", result.Text);

        console.StubInput($"{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{Enter}");
        result = await prompt.ReadLineAsync();
        Assert.Equal("", result.Text);
    }

    [Fact]
    public async Task ReadLine_WithHistory_DoNotSaveEmptyInput()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"Hello World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{UpArrow}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal("Hello World", result.Text);

        console.StubInput($"Hellow{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{UpArrow}{UpArrow}{Enter}");
        result = await prompt.ReadLineAsync();
        Assert.Equal("Hello World", result.Text);
    }

    [Fact]
    public async Task ReadLine_WithHistory_DoNotSaveDuplicateInputs()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"howdy{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"Hello World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"Hello World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"Hello World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{UpArrow}{UpArrow}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal("howdy", result.Text);

        // Current: howdy -> Hello World -> howdy.
        console.StubInput($"{UpArrow}{UpArrow}{UpArrow}{DownArrow}{Enter}");
        result = await prompt.ReadLineAsync();
        Assert.Equal("Hello World", result.Text);
    }

    [Fact]
    public async Task ReadLine_UnsubmittedText_IsNotLostWhenChangingHistory()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"Hello World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"this prompt is my persistent storage{UpArrow}{DownArrow}{Enter}");
        var result = await prompt.ReadLineAsync();

        Assert.Equal("this prompt is my persistent storage", result.Text);
    }

    [Fact]
    public async Task ReadLine_TypingOnHistory_ResetsHistory()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"one{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"two{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput(
            $"{UpArrow}{Backspace}{Backspace}{Backspace}three{Backspace}{Backspace}{Backspace}{Backspace}",
            $"{UpArrow}{Enter}"
        );
        var result = await prompt.ReadLineAsync();

        Assert.Equal("two", result.Text);
    }

    [Fact]
    public async Task ReadLine_NoPersistentHistory_DoesNotPersistAcrossPrompts()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);
        console.StubInput($"Entry One{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal("Entry One", result.Text);

        // second prompt, should not get history from first prompt
        console = ConsoleStub.NewConsole();
        prompt = new Prompt(console: console);
        console.StubInput($"{UpArrow}{Enter}");
        result = await prompt.ReadLineAsync();
        Assert.Equal("", result.Text); // did not navigate to "Entry One" above
    }

    [Fact]
    public async Task ReadLine_HistoryWithTextOnPrompt_FiltersHistory()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"one{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"two{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"o{UpArrow}{Enter}");
        var result = await prompt.ReadLineAsync();

        Assert.Equal("one", result.Text);
    }

    [Fact]
    public async Task ReadLine_PersistentHistory_PersistsAcrossPrompts()
    {
        var historyFile = Path.GetTempFileName();
        try
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
            console.StubInput($"Entry One{Enter}");
            var result = await prompt.ReadLineAsync();
            Assert.Equal("Entry One", result.Text);

            console = ConsoleStub.NewConsole();
            prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
            console.StubInput($"{UpArrow}{Enter}");
            result = await prompt.ReadLineAsync();
            Assert.Equal("Entry One", result.Text); // did not navigate to "Entry One" above
        }
        finally
        {
            File.Delete(historyFile);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/184
    /// </summary>
    [Fact]
    public async Task ReadLine_PersistentHistory_Deduplication()
    {
        var historyFile = Path.GetTempFileName();
        try
        {
            foreach (var input in new[] { "a", "b", "b", "b" })
            {
                var console = ConsoleStub.NewConsole();
                var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
                console.StubInput($"{input}{Enter}");
                var result = await prompt.ReadLineAsync();
                Assert.Equal(input, result.Text);
            }

            {
                var console = ConsoleStub.NewConsole();
                var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
                console.StubInput($"{UpArrow}{UpArrow}{Enter}");
                var result = await prompt.ReadLineAsync();
                Assert.Equal("a", result.Text);
            }
        }
        finally
        {
            File.Delete(historyFile);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/181
    /// </summary>
    [Fact]
    public async Task ReadLine_UpArrow_DoesNotCycleThroughHistory_WhenInMultilineStatement()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"a{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"{Shift}{Enter}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}b{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal($"b{Environment.NewLine}", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/188
    /// </summary>
    [Fact]
    public async Task ReturningBackFromFilteredHistory_ShouldGoBySameFilteredEntriesAsBefore()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"aa{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"b{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"c{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput(
            $"a{UpArrow}", //jumps to 'aa'
            $"{DownArrow}", //should go back right to 'a'
            $"{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal($"a", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/190
    /// </summary>
    [Fact]
    public async Task DirectHistoryCyclingThroughMultilineEntries()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"a{Shift}{Enter}1{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"b{Shift}{Enter}2{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"c{Shift}{Enter}3{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput(
            $"{UpArrow}", //jumps to 'c\n3'
            $"{UpArrow}", //jumps to 'b\n2'
            $"{UpArrow}", //jumps to 'a\n1'
            $"{DownArrow}", //should go back right to 'b\n2'
            $"{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal($"b{Environment.NewLine}2", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/187
    /// </summary>
    [Fact]
    public async Task GoingToHistoryWithNonMatchingFilter()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"a{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"b{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"c{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput(
            $"x{UpArrow}", //should go to 'c'
            $"{UpArrow}", //should go to 'b'
            $"{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal($"b", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/187
    /// </summary>
    [Fact]
    public async Task GoingBackToFutureWithNonMatchingFilter()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"a{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"b{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"c{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput(
            $"x{UpArrow}", //jumps to 'c'
            $"{UpArrow}", //jumps to 'b'
            $"{UpArrow}", //jumps to 'a'
            $"{DownArrow}", //should go back to 'b'
            $"{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal($"b", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/187
    /// </summary>
    [Fact]
    public async Task WeakerFilteringMatch()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"Console.WriteLine(){Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"Console.ReadLine(){Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"write{UpArrow}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal($"Console.WriteLine()", result.Text);
    }
}