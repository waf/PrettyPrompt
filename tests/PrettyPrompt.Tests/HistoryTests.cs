#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using NSubstitute;
using PrettyPrompt.Consoles;
using PrettyPrompt.History;
using PrettyPrompt.Panes;
using PrettyPrompt.TextSelection;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;
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
    public async Task ReadLine_WithHistory_CyclesWithCtrlPAndCtrlN()
    {
        // Ctrl+P / Ctrl+N are emacs aliases for the Up/Down history bindings
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"Hello World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"Howdy World{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"How ya' doin world{Enter}");
        await prompt.ReadLineAsync();

        // Ctrl+P three times walks back to the oldest entry; Ctrl+N steps forward one.
        console.StubInput($"{Control}{P}{Control}{P}{Control}{P}{Control}{N}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal("Howdy World", result.Text);
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
            await using (var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile))
            {
                console.StubInput($"Entry One{Enter}");
                var result = await prompt.ReadLineAsync();
                Assert.Equal("Entry One", result.Text);
            }

            console = ConsoleStub.NewConsole();
            await using (var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile))
            {
                console.StubInput($"{UpArrow}{Enter}");
                var result = await prompt.ReadLineAsync();
                Assert.Equal("Entry One", result.Text); // did not navigate to "Entry One" above
            }
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
                await using var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
                console.StubInput($"{input}{Enter}");
                var result = await prompt.ReadLineAsync();
                Assert.Equal(input, result.Text);
            }

            {
                var console = ConsoleStub.NewConsole();
                await using var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
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
    /// https://github.com/waf/CSharpRepl/issues/247
    /// When a single logical line is long enough to word-wrap across multiple display rows, the up-arrow
    /// should move the cursor up within those wrapped rows rather than navigating to a previous history
    /// entry. History navigation should only kick in once the cursor is already on the first display row.
    /// </summary>
    [Fact]
    public async Task ReadLine_UpArrow_MovesCursorWithinWrappedLine_InsteadOfCyclingHistory()
    {
        var console = ConsoleStub.NewConsole(width: 20); // code area is 18 columns wide after the "> " prompt
        var prompt = new Prompt(console: console);

        console.StubInput($"old entry{Enter}");
        await prompt.ReadLineAsync();

        // a single logical line (no newlines) long enough to wrap across multiple display rows
        var wrapped = new string('a', 40);
        console.StubInput($"{wrapped}{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput(
            $"{UpArrow}",    // loads the wrapped entry; cursor lands at its end, on the last display row
            $"{LeftArrow}",  // moves the cursor and ends the active history cycle
            $"{UpArrow}",    // cursor is not on the first display row, so this moves it up within the wrapped
                             // entry instead of navigating back to "old entry"
            $"{Enter}");
        var result = await prompt.ReadLineAsync();

        Assert.Equal(wrapped, result.Text);
    }

    /// <summary>
    /// Follow-up to https://github.com/waf/CSharpRepl/issues/247
    /// After recalling a multiline history entry and moving the cursor up within it, pressing Down should walk
    /// the cursor back down through the entry and then return to the original (empty) prompt, rather than
    /// trapping the user on the recalled entry.
    /// </summary>
    [Fact]
    public async Task ReadLine_DownArrow_ReturnsToEmptyPrompt_AfterNavigatingWithinRecalledMultilineEntry()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"a{Shift}{Enter}b{Enter}"); // submit the multiline entry "a\nb"
        await prompt.ReadLineAsync();

        console.StubInput(
            $"{UpArrow}",    // recall "a\nb"; cursor lands on the last display row
            $"{UpArrow}",    // no older history to cycle to, so this moves the cursor up within the entry
            $"{DownArrow}",  // move the cursor back down within the entry
            $"{DownArrow}",  // cursor is on the last row again, so this returns to the empty prompt
            $"x{Enter}");    // typing on the now-empty prompt
        var result = await prompt.ReadLineAsync();

        Assert.Equal("x", result.Text);
    }

    /// <summary>
    /// Follow-up to https://github.com/waf/CSharpRepl/issues/247
    /// Moving the cursor around a recalled (word-wrapped) entry must not detach us from history. After
    /// navigating up/down/left/right within the wrapped entry, pressing Down from its last row should still
    /// return to the original (empty) prompt rather than trapping the user on the recalled entry.
    /// </summary>
    [Fact]
    public async Task ReadLine_DownArrow_ReturnsToEmptyPrompt_AfterMovingCursorWithinRecalledWrappedEntry()
    {
        var console = ConsoleStub.NewConsole(width: 20); // code area is 18 columns wide after the "> " prompt
        var prompt = new Prompt(console: console);

        var wrapped = new string('a', 40); // a single logical line that wraps across multiple display rows
        console.StubInput($"{wrapped}{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput(
            $"{UpArrow}",     // recall the wrapped entry; cursor lands at its end (last display row)
            $"{LeftArrow}",   // move the cursor (must NOT detach from history)
            $"{UpArrow}",     // move the cursor up within the wrapped entry
            $"{DownArrow}",   // move the cursor back down within the entry
            $"{RightArrow}",  // move the cursor back to the end of the line
            $"{DownArrow}",   // cursor is on the last row, so this returns to the empty prompt
            $"x{Enter}");     // typing on the now-empty prompt
        var result = await prompt.ReadLineAsync();

        Assert.Equal("x", result.Text);
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
    /// https://github.com/waf/PrettyPrompt/issues/194
    /// </summary>
    [Fact]
    public async Task GoingBackToFutureWithNonMatchingFilter2()
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
            $"c{UpArrow}",
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

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/194
    /// </summary>
    [Fact]
    public async Task SkipExactMatches()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        console.StubInput($"a{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"b{Enter}");
        await prompt.ReadLineAsync();

        console.StubInput($"b{UpArrow}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.Equal($"a", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/266
    /// </summary>
    [Fact]
    public async Task History_InvalidBase64Value_DoesNotCrash()
    {
        // set up history log with invalid data on the second line (it's not base64 encoded)
        var historyPath = Path.GetTempFileName();
        File.WriteAllLines(historyPath, new[]
        {
            Base64Encode("var x = 1;"),
            "banana",
            Base64Encode("var y = 2;")
        });

        // set up key listeners
        var history = new HistoryLog(historyPath, new KeyBindings());
        var codePane = new CodePane(
            Substitute.For<IConsole>(),
            new PromptConfiguration(),
            Substitute.For<IPromptCallbacks>(),
            new WrappedClipboard(Substitute.For<IClipboard>())
        );
        history.Track(codePane);

        // press 'up arrow' twice -- we should never encounter the invalid line, and there shouldn't be any crashes.
        await history.OnKeyUp(PressUpArrow(), CancellationToken.None);
        Assert.Equal("var y = 2;", codePane.Document.GetText());
        await history.OnKeyUp(PressUpArrow(), CancellationToken.None);
        Assert.Equal("var x = 1;", codePane.Document.GetText());


        static string Base64Encode(string input) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(input));

        static KeyPress PressUpArrow() =>
            // return a new object every key press because keypresses can be marked as 'handled'
            new KeyPress(new ConsoleKeyInfo('\0', UpArrow, shift: false, alt: false, control: false));
    }
}