using PrettyPrompt.Consoles;
using Xunit;
using NSubstitute;
using System.Threading.Tasks;
using System.Collections.Generic;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static System.Environment;
using static PrettyPrompt.AnsiEscapeCodes;

namespace PrettyPrompt.Tests
{
    public class PromptTests
    {
        [Fact]
        public async Task ReadLine_TypeSimpleString_GetSimpleString()
        {
            IConsole console = NewConsole();
            console.Input($"Hello World{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLine("> ");

            Assert.True(result.Success);
            Assert.Equal("Hello World", result.Text);
        }

        [Fact]
        public async Task ReadLine_Abort_NoResult()
        {
            IConsole console = NewConsole();
            console.Input($"Hello World{Control}c");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLine("> ");

            Assert.False(result.Success);
            Assert.Empty(result.Text);
        }

        [Fact]
        public async Task ReadLine_WordWrap()
        {
            // window width of 5, with a 2 char prompt.
            var console = NewConsole(width: 5);
            console.Input($"111222333{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLine("> ");

            Assert.True(result.Success);
            Assert.Equal("111222333", result.Text);

            var finalOutput = GetFinalOutput(console.AllOutput());

            Assert.Equal(
                expected: MoveCursorToPosition(1, 1) + ClearToEndOfScreen +
                          "> 111" +
                          "  222" + 
                          "  333" + 
                          MoveCursorToPosition(row: 4, column: 3),
                actual: finalOutput
            );
        }

        [Fact]
        public async Task ReadLine_HorizontalNavigationKeys()
        {
            var console = NewConsole();
            console.Input(
                $"pretty{Backspace}{Backspace}{Home}{LeftArrow}{RightArrow}{RightArrow}{Delete}omp{RightArrow}!{RightArrow}{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLine("> ");

            Assert.Equal("prompt!", result.Text);
        }

        [Fact]
        public async Task ReadLine_VerticalNavigationKeys()
        {
            var console = NewConsole();
            console.Input(
                $"pretty{Shift}{Enter}",
                $"unit-tested{Shift}{Enter}",
                $"prompt",
                $"{UpArrow}{RightArrow}{RightArrow}{RightArrow}{RightArrow}{RightArrow}?",
                $"{UpArrow} well",
                $"{DownArrow}{DownArrow}!{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLine("> ");

            Assert.Equal($"pretty well{NewLine}unit-tested?{NewLine}prompt!", result.Text);
        }

        private static string GetFinalOutput(IReadOnlyList<string> output) =>
            output[^2]; // second to last. The last is always the newline drawn after the prompt is submitted

        private static IConsole NewConsole(int width = 100)
        {
            var console = Substitute.For<IConsole>();
            console.BufferWidth.Returns(width);
            return console;
        }
    }
}

