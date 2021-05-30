#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;
using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static System.Environment;

namespace PrettyPrompt.Tests
{
    public class CompletionTests
    {
        [Fact]
        public async Task ReadLine_SingleCompletion()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"Aa{Enter}{Enter}");

            var prompt = new Prompt(
                callbacks: new PromptCallbacks
                {
                    CompletionCallback = CompletionTestData.CompletionHandlerAsync
                },
                console: console
            );

            var result = await prompt.ReadLineAsync("> ");

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

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Aardvark Zebra Alpaca", result.Text);
        }

        [Fact]
        public async Task ReadLine_MultilineCompletion()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"Aa{Enter}{Shift}{Enter}Z{Control}{Spacebar}{Enter}{Enter}");

            var prompt = ConfigurePrompt(console);

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.IsSuccess);
            Assert.Equal($"Aardvark{NewLine}Zebra", result.Text);
        }

        [Fact]
        public async Task ReadLine_CompletionMenu_AutoOpens()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"A{Enter}{Shift}{Enter}Z{Enter}{Enter}");

            Prompt prompt = ConfigurePrompt(console);

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.IsSuccess);
            Assert.Equal($"Aardvark{NewLine}Zebra", result.Text);
        }

        [Fact]
        public async Task ReadLine_FullyTypeCompletion_CanOpenAgain()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"Aardvark {Control}{Spacebar}{Enter}{Enter}");

            var prompt = ConfigurePrompt(console);

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.IsSuccess);
            Assert.Equal($"Aardvark Aardvark", result.Text);
        }

        [Fact]
        public async Task ReadLine_EmptyPrompt_AutoOpens()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"{Control}{Spacebar}{Enter}{Enter}");

            var prompt = ConfigurePrompt(console);

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.IsSuccess);
            Assert.Equal($"Aardvark", result.Text);
        }

        [Fact]
        public async Task ReadLine_OpenWindowAtBeginningOfPrompt_Opens()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"a{LeftArrow} {LeftArrow}a{Enter}{Enter}");

            var prompt = ConfigurePrompt(console);

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.IsSuccess);
            Assert.Equal($"Aardvark a", result.Text);
        }

        [Fact]
        public async Task ReadLine_CompletionWithNoMatches_DoesNotAutoComplete()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"A{Enter} Q{Enter}"); // first {Enter} selects an autocompletion, second {Enter} submits because there are no completions.

            var prompt = ConfigurePrompt(console);

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.IsSuccess);
            Assert.Equal($"Aardvark Q", result.Text);
        }

        private static Prompt ConfigurePrompt(IConsole console) =>
            new Prompt(
                callbacks: new PromptCallbacks
                {
                    CompletionCallback = CompletionTestData.CompletionHandlerAsync
                },
                console: console
            );
    }
}
