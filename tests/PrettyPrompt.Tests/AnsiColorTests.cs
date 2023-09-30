using PrettyPrompt.Highlighting;
using Xunit;

namespace PrettyPrompt.Tests;

public class AnsiColorTests
{
    [Theory]
    [InlineData(0, 0, 0, "#000000")]
    [InlineData(13, 183, 227, "#0DB7E3")]
    [InlineData(255, 255, 255, "#FFFFFF")]
    public void RgbToStringAndParse(byte r, byte g, byte b, string text)
    {
        Assert.Equal(text, AnsiColor.Rgb(r, g, b).ToString());

        Assert.True(AnsiColor.TryParse(text, out var parsedColor));
        Assert.Equal(text, parsedColor.ToString());
    }

    [Fact]
    public void WllKnownColorToStringAndParse()
    {
        Assert.True(AnsiColor.TryParse("Black", out var parsedColor));
        Assert.Equal(AnsiColor.Black.ToString(), parsedColor.ToString());

        Assert.True(AnsiColor.TryParse("white", out parsedColor));
        Assert.Equal(AnsiColor.White.ToString(), parsedColor.ToString());

        Assert.True(AnsiColor.TryParse("GREEN", out parsedColor));
        Assert.Equal(AnsiColor.Green.ToString(), parsedColor.ToString());
    }
}
