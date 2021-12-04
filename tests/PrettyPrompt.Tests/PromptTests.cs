#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static System.Environment;
using System;
using System.Collections.Generic;
using PrettyPrompt.Highlighting;
using System.Linq;

namespace PrettyPrompt.Tests
{
    public class PromptTests
    {
        [Fact]
        public async Task ReadLine_TypeSimpleString_GetSimpleString()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"Hello World{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal("Hello World", result.Text);
        }

        [Fact]
        public async Task ReadLine_ControlEnter_IsHardEnter()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"Hello World{Control}{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.True(result.IsSuccess);
            Assert.True(result.IsHardEnter);
            Assert.Equal("Hello World", result.Text);
        }

        [Fact]
        public async Task ReadLine_WhitespacePrompt_ReturnsWhitespace()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"  {Enter}");

            // add completion handler, as it has caused problem when completing all whitespace prompts
            var prompt = new Prompt(
                callbacks: new PromptCallbacks
                {
                    CompletionCallback = new CompletionTestData().CompletionHandlerAsync
                },
                console: console
            );

            var result = await prompt.ReadLineAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal("  ", result.Text);
        }

        [Fact]
        public async Task ReadLine_Abort_NoResult()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"Hello World{Control}c");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.False(result.IsSuccess);
            Assert.Empty(result.Text);
        }

        [Fact]
        public async Task ReadLine_WordWrap()
        {
            // window width of 5, with a 2 char prompt.
            var console = ConsoleStub.NewConsole(width: 5);
            console.StubInput($"111222333{Control}{L}{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal("111222333", result.Text);

            var finalOutput = console.GetFinalOutput();

            Assert.Equal(
                expected: "111\n" + MoveCursorLeft(2) +
                          "222\n" + MoveCursorLeft(2) +
                          "333\n" + MoveCursorLeft(2),
                actual: finalOutput
            );
        }

        [Fact]
        public async Task ReadLine_HorizontalNavigationKeys()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"pretty{Backspace}{Backspace}{Home}{LeftArrow}{RightArrow}{RightArrow}{Delete}omp{RightArrow}{Home}{End}!{RightArrow}{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.Equal("prompt!", result.Text);
        }

        [Fact]
        public async Task ReadLine_HomeEndKeys_NavigateLines()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"{Shift}{Enter}{Shift}{Enter}hello{Home}{Delete}H{End}!{Shift}{Enter}",
                $"world{Control}{Home}I say:{Control}{End}!{Home}{Delete}W{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.Equal($"I say:{NewLine}{NewLine}Hello!{NewLine}World!", result.Text);
        }

        [Fact]
        public async Task ReadLine_VerticalNavigationKeys()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"pretty{Shift}{Enter}",
                $"unit-tested{Shift}{Enter}",
                $"prompt",
                $"{UpArrow}{RightArrow}{RightArrow}{RightArrow}{RightArrow}{RightArrow}?",
                $"{UpArrow} well",
                $"{DownArrow}{DownArrow}!{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.Equal($"pretty well{NewLine}unit-tested?{NewLine}prompt!", result.Text);
        }

        [Fact]
        public async Task ReadLine_NextWordPrevWordKeys()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"aaaa bbbb 5555{Shift}{Enter}",
                $"dddd x5x5 foo.bar{Shift}{Enter}",
                $"{UpArrow}{Control}{RightArrow}{Control}{RightArrow}{Control}{RightArrow}{Control}{RightArrow}lum",
                $"{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}{Backspace}{Tab}",
                $"{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.Equal($"aaaa bbbb 5555{NewLine}dddd x5x5    foo.lumbar{NewLine}", result.Text);
        }

        [Fact]
        public async Task ReadLine_DeleteWordPrevWordKeys()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"aaaa bbbb cccc{Shift}{Enter}",
                $"dddd eeee ffff{Shift}{Enter}",
                $"{UpArrow}{Control}{Delete}{Control}{Backspace}",
                $"{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.Equal($"aaaa bbbb eeee ffff{NewLine}", result.Text);
        }

        [Fact]
        public async Task ReadLine_TypeReallyQuickly_DoesNotDropKeyPresses()
        {
            var console = ConsoleStub.NewConsole();
            // it's possible that if keys are pressed simultaneously / quickly, we'll still have
            // some keys in the buffer after calling Console.ReadKey()
            console.KeyAvailable.Returns(true, true, false);
            console.StubInput($"abc{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            Assert.Equal($"abc", result.Text);
        }

        [Fact]
        public async Task ReadLine_Paste_DoesNotRepeatedlySyntaxHighlight()
        {
            var console = ConsoleStub.NewConsole();
            console.KeyAvailable.Returns(true, " am pasting conten".Select(_ => true).Append(false).ToArray());
            console.StubInput($"I am pasting content{LeftArrow}{RightArrow}{Enter}");

            int syntaxHighlightingInvocations = 0;

            var prompt = new Prompt(callbacks: new PromptCallbacks
            {
                HighlightCallback = text =>
                {
                    syntaxHighlightingInvocations++;
                    return Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>());
                }
            }, console: console);

            var result = await prompt.ReadLineAsync();

            Assert.Equal("I am pasting content", result.Text);
            Assert.Equal(1, syntaxHighlightingInvocations);
        }

        [Fact]
        public async Task ReadLine_Paste_TrimsLeadingIndentation()
        {
            var console = ConsoleStub.NewConsole();
            
            console.KeyAvailable
                .Returns(true, $"   indent\r        more indent\r\r    inden".Select(_ => true).Append(false).ToArray());
            console.StubInput($"    indent\r        more indent\r\r    indent{Enter}");

            var prompt = new Prompt(console: console);

            var result = await prompt.ReadLineAsync();

            Assert.Equal($"indent{NewLine}    more indent{NewLine}{NewLine}indent", result.Text);
        }

        [Fact]
        public async Task ReadLine_KeyPressCallback_IsInvoked()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"I like apple{Control}{LeftArrow}{Control}{LeftArrow}{F1}{Enter}");

            string input = null;
            int? caret = null;
            var prompt = new Prompt(callbacks: new PromptCallbacks
            {
                KeyPressCallbacks =
                {
                    [F1] = (inputArg, caretArg) => { input = inputArg; caret = caretArg; return Task.FromResult<KeyPressCallbackResult>(null); }
                }
            }, console: console);

            _ = await prompt.ReadLineAsync();

            Assert.Equal("I like apple", input);
            Assert.Equal(2, caret);
        }

        [Fact]
        public async Task ReadLine_KeyPressCallbackReturnsOutput_IsReturned()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"I like apple{Control}{LeftArrow}{Control}{LeftArrow}{F2}{Enter}");

            var callbackOutput = new KeyPressCallbackResult("", "Callback output!");
            var prompt = new Prompt(callbacks: new PromptCallbacks
            {
                KeyPressCallbacks =
                {
                    [F2] = (inputArg, caretArg) => {
                        return Task.FromResult(callbackOutput);
                    }
                }
            }, console: console);

            var result = await prompt.ReadLineAsync();

            Assert.Equal(callbackOutput, result);
        }
    }
}

