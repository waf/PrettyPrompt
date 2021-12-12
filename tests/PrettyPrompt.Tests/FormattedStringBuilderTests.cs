using PrettyPrompt.Highlighting;
using Xunit;

namespace PrettyPrompt.Tests;

public class FormattedStringBuilderTests
{
    [Fact]
    public void Empty()
    {
        var sb = new FormattedStringBuilder();

        sb.Append("")
          .Append(FormattedString.Empty)
          .Append(new FormattedString(""))
          .Append(new FormattedString("", new ConsoleFormat(Foreground: AnsiColor.Red)))
          .Append(new FormattedString("", new FormatSpan(0, 0, new ConsoleFormat(Foreground: AnsiColor.Red))));

        Assert.Equal(0, sb.Length);
        Assert.Equal(FormattedString.Empty, sb.ToFormattedString());

        sb.Clear();

        Assert.Equal(0, sb.Length);
        Assert.Equal(FormattedString.Empty, sb.ToFormattedString());
    }

    [Fact]
    public void Append()
    {
        var sb = new FormattedStringBuilder();

        sb.Append("1")
          .Append(FormattedString.Empty)
          .Append("2", new FormatSpan(0, 1, new ConsoleFormat(Foreground: AnsiColor.Red)))
          .Append("34", new FormatSpan(0, 1, new ConsoleFormat(Foreground: AnsiColor.Green)), new FormatSpan(1, 1, new ConsoleFormat(Foreground: AnsiColor.Yellow)));

        Assert.Equal(4, sb.Length);
        Assert.Equal(
            new FormattedString(
                "1234", 
                new FormatSpan(1, 1, new ConsoleFormat(Foreground: AnsiColor.Red)),
                new FormatSpan(2, 1, new ConsoleFormat(Foreground: AnsiColor.Green)),
                new FormatSpan(3, 1, new ConsoleFormat(Foreground: AnsiColor.Yellow))), 
            sb.ToFormattedString());

        sb.Clear();

        Assert.Equal(0, sb.Length);
        Assert.Equal(FormattedString.Empty, sb.ToFormattedString());
    }
}
