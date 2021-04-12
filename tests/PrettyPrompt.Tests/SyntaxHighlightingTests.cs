using System.Threading.Tasks;
using Xunit;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static System.ConsoleKey;

namespace PrettyPrompt.Tests
{
    public class SyntaxHighlightingTests
    {
        [Fact]
        public async Task ReadLine_SyntaxHighlight()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"red green nocolor blue{Enter}");

            var prompt = new Prompt(highlightHandler: SyntaxHighlighterTestData.HighlightHandlerAsync, console: console);

            var result = await prompt.ReadLineAsync("> ");

            Assert.True(result.Success);
            Assert.Equal("red green nocolor blue", result.Text);
            var output = console.GetAllOutput();

            // although the words are typed character-by-character, we should still "go back" and redraw
            // it once we know the word should be drawn in a syntax-highlighted color.
            Assert.Contains(
                MoveCursorToPosition(1, 3) + BrightRed + "red" + MoveCursorToPosition(1, 6) + ResetFormatting, // prompt is the first two columns
                output
            );
            Assert.Contains(
                MoveCursorToPosition(1, 7) + BrightGreen + "green" + MoveCursorToPosition(1, 12) + ResetFormatting,
                output
            );
            Assert.Contains(
                MoveCursorToPosition(1, 21) + BrightBlue + "blue" + MoveCursorToPosition(1, 25) + ResetFormatting,
                output
            );

            Assert.DoesNotContain("nocolor", output); // it should output character by character as we type; never the whole string at once.
        }
    }
}
