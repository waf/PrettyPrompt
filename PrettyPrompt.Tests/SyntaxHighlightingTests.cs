using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;
using static PrettyPrompt.AnsiEscapeCodes;

namespace PrettyPrompt.Tests
{
    public class SyntaxHighlightingTests
    {
        [Fact]
        public async Task ReadLine_SyntaxHighlight()
        {
            var console = ConsoleStub.NewConsole();
            console.Input($"red green nocolor blue{Enter}");

            var prompt = new Prompt(highlightHandler: SyntaxHighlighterTestData.HighlightHandlerAsync, console: console);

            var result = await prompt.ReadLine("> ");

            Assert.True(result.Success);
            Assert.Equal("red green nocolor blue", result.Text);
            var output = console.GetFinalOutput();
            Assert.Equal(
                expected: MoveCursorToPosition(1, 1) + ClearToEndOfScreen +
                          $"> {BrightRed}red{ResetFormatting} {BrightGreen}green{ResetFormatting} nocolor {BrightBlue}blue{ResetFormatting}" +
                          MoveCursorToPosition(row: 1, column: 25),
                actual: output
            );
        }

    }
}
