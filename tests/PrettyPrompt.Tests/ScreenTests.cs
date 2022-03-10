using PrettyPrompt.Consoles;
using PrettyPrompt.Rendering;
using Xunit;

namespace PrettyPrompt.Tests;

public class ScreenTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("ab", 2)]
    [InlineData("abc", 3)]
    
    [InlineData("书", 2)]
    [InlineData("a书", 3)]
    [InlineData("a书bc", 5)]
    [InlineData("a书上bc", 7)]

    [InlineData("😀", 2)]
    [InlineData("a😁", 3)]
    [InlineData("a😉bc", 5)]
    [InlineData("a😐🙄bc", 7)]
    public void ScreenCursorPositionTest(string text, int expectedCursorPosition)
    {
        var screen = new Screen(
            width: 128,
            height: 16,
            new ConsoleCoordinate(0, text.Length), //cursor at the end of the text
            new ScreenArea(
                new ConsoleCoordinate(0, 0),
                new[]
                {
                    new Row(Cell.FromText(text))
                }));

        Assert.Equal(new ConsoleCoordinate(0, expectedCursorPosition), screen.Cursor);
    }
}
