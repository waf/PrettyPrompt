using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static System.Environment;
using static PrettyPrompt.AnsiEscapeCodes;

namespace PrettyPrompt.Tests
{
    public class CompletionTests
    {
        private static readonly IReadOnlyCollection<string> completions = new[]
        {
            "Aardvark", "Albatross", "Alligator", "Alpaca", "Ant", "Anteater", "Zebra"
        };
        
        private static Task<IReadOnlyList<Completion>> CompletionHandlerAsync(string text, int caret)
        {
            var textUntilCaret = text.Substring(0, caret);
            var previousWordStart = textUntilCaret.LastIndexOfAny(new[] { ' ', '\n', '.', '(', ')' });
            var typedWord = previousWordStart == -1
                ? textUntilCaret
                : textUntilCaret.Substring(previousWordStart + 1);
            return Task.FromResult<IReadOnlyList<Completion>>(
                completions
                    .Where(c => c.StartsWith(typedWord))
                    .Select(c => new Completion
                    {
                        StartIndex = previousWordStart + 1,
                        ReplacementText = c
                    })
                    .ToArray()
            );
        }

        [Fact]
        public async Task ReadLine_SingleCompletion()
        {
            var console = ConsoleStub.NewConsole();
            console.Input($"Aa{Enter}{Enter}");

            var prompt = new Prompt(CompletionHandlerAsync, console: console);

            var result = await prompt.ReadLine("> ");

            Assert.True(result.Success);
            Assert.Equal("Aardvark", result.Text);
        }

        [Fact]
        public async Task ReadLine_MultipleCompletion()
        {
            var console = ConsoleStub.NewConsole();
            // complete 3 animals. For the third animal, start completing Alligator, but then backspace and complete as Alpaca instead.
            console.Input($"Aa{Enter} Z{Tab} Alli{Backspace}{Backspace}{DownArrow}{DownArrow}{RightArrow}{Enter}");

            var prompt = new Prompt(CompletionHandlerAsync, console: console);

            var result = await prompt.ReadLine("> ");

            Assert.True(result.Success);
            Assert.Equal("Aardvark Zebra Alpaca", result.Text);
        }

        [Fact]
        public async Task ReadLine_MultilineCompletion()
        {
            var console = ConsoleStub.NewConsole();
            console.Input($"Aa{Enter}{Shift}{Enter}Z{Control}{Spacebar}{Enter}{Enter}");

            var prompt = new Prompt(CompletionHandlerAsync, console: console);

            var result = await prompt.ReadLine("> ");

            Assert.True(result.Success);
            Assert.Equal($"Aardvark{NewLine}Zebra", result.Text);
        }
    }
}
