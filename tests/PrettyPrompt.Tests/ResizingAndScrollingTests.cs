#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static PrettyPrompt.Tests.ConsoleStub;

namespace PrettyPrompt.Tests
{
    public class ResizingAndScrollingTests
    {
        /// <summary>
        /// Triggered crash: https://github.com/waf/PrettyPrompt/issues/23
        /// </summary>
        [Fact]
        public async Task ScrollDown_PressUp()
        {
            var console = NewConsole();

            console.StubInput(
                Input($" ", () => console.WindowTop.Returns(10)), //scroll down by 10
                Input($"{UpArrow}{Enter}"));

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
        }

        /// <summary>
        /// Triggered crash (mentioned in https://github.com/waf/PrettyPrompt/issues/23).
        /// </summary>
        [Fact]
        public async Task WriteNewLines_ScrollUp_WriteLetter()
        {
            var console = NewConsole(height: 3);

            console.StubInput(
                Input($"{Shift}{Enter}"),
                Input($"{Shift}{Enter}"),
                Input($"{Shift}{Enter}"),
                Input($"{Shift}{Enter}", () => console.WindowTop.Returns(1)), //new line scrolls down 1 row
                Input($"{Shift}{Enter}", () => console.WindowTop.Returns(2)), //new line scrolls down 1 row
                Input($"{Shift}{Enter}", () => console.WindowTop.Returns(3)), //new line scrolls down 1 row
                Input($"{Shift}{Enter}", () => console.WindowTop.Returns(4)), //new line scrolls down 1 row
                Input($" ", () => console.WindowTop.Returns(0)), //scroll up to start
                Input($"{A}{Enter}"));

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
        }

        /// <summary>
        /// Triggered crash: https://github.com/waf/PrettyPrompt/issues/29
        /// </summary>
        [Fact]
        public async Task WindowNarrowerThanPromt()
        {
            var console = NewConsole(width: 5);
            console.StubInput(Input($"{Enter}"));
            var prompt = new Prompt(console: console, theme: new PromptTheme(prompt: "LOOOONG_PROMPT--->>"));
            await prompt.ReadLineAsync();
        }
    }
}