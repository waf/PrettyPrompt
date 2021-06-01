#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.IO;
using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;

namespace PrettyPrompt.Tests
{
    public class HistoryTests
    {
        [Fact]
        public async Task ReadLine_WithHistory_DoesNothing()
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            console.StubInput($"{UpArrow}{UpArrow}{DownArrow}{DownArrow}yo world{Enter}");
            var result = await prompt.ReadLineAsync("> ");

            // no exceptions, even though we cycled through history when there was no history to cycle through
            Assert.Equal("yo world", result.Text);
        }

        [Fact]
        public async Task ReadLine_WithHistory_CyclesThroughHistory()
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            console.StubInput($"Hello World{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"Howdy World{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"How ya' doin world{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{UpArrow}{UpArrow}{UpArrow}{DownArrow}{Enter}");
            var result = await prompt.ReadLineAsync("> ");

            Assert.Equal("Howdy World", result.Text);
        }

        [Fact]
        public async Task ReadLine_WithHistory_DoNotSaveEmptyInput()
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            console.StubInput($"Hello World{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{UpArrow}{Enter}");
            var result = await prompt.ReadLineAsync("> ");
            Assert.Equal("Hello World", result.Text);

            console.StubInput($"Hellow{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{UpArrow}{UpArrow}{Enter}");
            result = await prompt.ReadLineAsync("> ");
            Assert.Equal("Hello World", result.Text);
        }

        [Fact]
        public async Task ReadLine_WithHistory_DoNotSaveDuplicateInputs()
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            console.StubInput($"howdy{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"Hello World{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"Hello World{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"Hello World{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"{UpArrow}{UpArrow}{Enter}");
            var result = await prompt.ReadLineAsync("> ");
            Assert.Equal("howdy", result.Text);

            // Current: howdy -> Hello World -> howdy.
            console.StubInput($"{UpArrow}{UpArrow}{UpArrow}{DownArrow}{Enter}");
            result = await prompt.ReadLineAsync("> ");
            Assert.Equal("Hello World", result.Text);
        }

        [Fact]
        public async Task ReadLine_UnsubmittedText_IsNotLostWhenChangingHistory()
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            console.StubInput($"Hello World{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"this prompt is my persistent storage{UpArrow}{DownArrow}{Enter}");
            var result = await prompt.ReadLineAsync("> ");

            Assert.Equal("this prompt is my persistent storage", result.Text);
        }

        [Fact]
        public async Task ReadLine_TypingOnHistory_ResetsHistory()
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);

            console.StubInput($"And a one{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"And a two{Enter}");
            await prompt.ReadLineAsync("> ");

            console.StubInput($"And a one, two, three...{UpArrow}{Backspace}{Backspace}{Backspace}three{UpArrow}{UpArrow}{DownArrow}{DownArrow}{Enter}");
            var result = await prompt.ReadLineAsync("> ");

            Assert.Equal("And a three", result.Text);
        }

        [Fact]
        public async Task ReadLine_NoPersistentHistory_DoesNotPersistAcrossPrompts()
        {
            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console);
            console.StubInput($"Entry One{Enter}");
            var result = await prompt.ReadLineAsync("> ");
            Assert.Equal("Entry One", result.Text);

            // second prompt, should not get history from first prompt
            console = ConsoleStub.NewConsole();
            prompt = new Prompt(console: console);
            console.StubInput($"{UpArrow}{Enter}");
            result = await prompt.ReadLineAsync("> ");
            Assert.Equal("", result.Text); // did not navigate to "Entry One" above
        }

        [Fact]
        public async Task ReadLine_PersistentHistory_PersistsAcrossPrompts()
        {
            var historyFile = Path.GetTempFileName();

            var console = ConsoleStub.NewConsole();
            var prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
            console.StubInput($"Entry One{Enter}");
            var result = await prompt.ReadLineAsync("> ");
            Assert.Equal("Entry One", result.Text);

            console = ConsoleStub.NewConsole();
            prompt = new Prompt(console: console, persistentHistoryFilepath: historyFile);
            console.StubInput($"{UpArrow}{Enter}");
            result = await prompt.ReadLineAsync("> ");
            Assert.Equal("Entry One", result.Text); // did not navigate to "Entry One" above
        }
    }
}
