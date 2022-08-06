using PrettyPrompt.Consoles;
using Xunit;

namespace PrettyPrompt.Tests;

public class ConsoleCoordinateTests
{
    [Fact]
    public void ToCaret_Empty()
    {
        Assert.Equal(0, ConsoleCoordinate.Zero.ToCaret(new[] { "" }));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(1, 2, 3)]
    [InlineData(1, 3, 4)]

    [InlineData(2, 0, 5)]
    [InlineData(2, 1, 6)]

    [InlineData(3, 0, 7)]

    [InlineData(4, 0, 8)]
    [InlineData(4, 1, 9)]
    [InlineData(4, 2, 10)]
    [InlineData(4, 3, 11)]
    [InlineData(4, 4, 12)]
    [InlineData(4, 5, 13)]

    [InlineData(5, 0, 14)]
    [InlineData(5, 1, 15)]
    [InlineData(5, 2, 16)]
    [InlineData(5, 3, 17)]
    [InlineData(5, 4, 18)]
    [InlineData(5, 5, 19)]
    [InlineData(5, 6, 20)]
    [InlineData(5, 7, 21)]
    [InlineData(5, 8, 22)]

    [InlineData(6, 0, 23)]
    [InlineData(6, 1, 24)]
    [InlineData(6, 2, 25)]

    [InlineData(7, 0, 26)]
    public void ToCaret(int row, int column, int caret)
    {
        const string Text =
@"
aaa
b

ccccc
      dd
 e
";

        Assert.Equal(caret, new ConsoleCoordinate(row, column).ToCaret(Text.Replace("\r", "").Split('\n')));
    }
}
