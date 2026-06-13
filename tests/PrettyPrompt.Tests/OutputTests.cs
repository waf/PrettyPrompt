using System;
using PrettyPrompt.Highlighting;
using Xunit;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static PrettyPrompt.Highlighting.AnsiColor;

namespace PrettyPrompt.Tests;

public class OutputTests
{
    [Fact]
    public void RenderAnsiOutput_PlainText()
    {
        var output = Prompt.RenderAnsiOutput("here is some output", System.Array.Empty<FormatSpan>(), 100);

        Assert.Equal("here is some output" + GetMoveCursorLeft(19), output);
    }

    [Fact]
    public void RenderAnsiOutput_GivenFormat_AppliesAnsiEscapeSequences()
    {
        var format1 = new ConsoleFormat(Foreground: Red);
        var format2 = new ConsoleFormat(Foreground: Green);
        var output = Prompt.RenderAnsiOutput("here is some output", new[]
        {
                new FormatSpan(0, 4, format1),
                new FormatSpan(8, 4, format2),
        }, 100);

        Assert.Equal(
            ToAnsiEscapeSequenceSlow(format1) + "here" + Reset + " is " + ToAnsiEscapeSequenceSlow(format2) + "some" + Reset + " output" + GetMoveCursorLeft(19),
            output
        );
    }

    [Fact]
    public void RenderAnsiOutput_WindowsLineEndings_RenderSameAsUnixLineEndings()
    {
        var unix = Prompt.RenderAnsiOutput("line one\nline two\nline three", System.Array.Empty<FormatSpan>(), 100);
        var windows = Prompt.RenderAnsiOutput("line one\r\nline two\r\nline three", System.Array.Empty<FormatSpan>(), 100);

        Assert.Equal(unix, windows);
    }

    [Fact]
    public void RenderAnsiOutput_WindowsLineEndingsWithFormat_SpansUseOriginalTextOffsets()
    {
        var format = new ConsoleFormat(Foreground: Red);

        // spans are keyed to offsets in the text as supplied, so "two" in "one\r\ntwo" starts at 5 (not 4).
        var unix = Prompt.RenderAnsiOutput("one\ntwo", new[] { new FormatSpan(4, 3, format) }, 100);
        var windows = Prompt.RenderAnsiOutput("one\r\ntwo", new[] { new FormatSpan(5, 3, format) }, 100);

        Assert.Equal(unix, windows);
        Assert.Contains(ToAnsiEscapeSequenceSlow(format) + "two", windows);
    }

    [Fact]
    public void RenderAnsiOutput_GivenFormatAndWrapping_AppliesAnsiEscapeSequences()
    {
        var format1 = new ConsoleFormat(Foreground: Red);
        var format2 = new ConsoleFormat(Foreground: Green);
        var output = Prompt.RenderAnsiOutput("here is some output", new[]
        {
                new FormatSpan(0, 4, format1),
                new FormatSpan(8, 4, format2),
        }, 4);

        // After "\n", Windows preserves the column and moves left 3 back to the wrapped line's start. On other
        // platforms the terminal returns to column 1 (ONLCR); the origin here is column 0, so the renderer's
        // reset-to-column-1 leaves a 1-column move-left. The final move (after "put", no newline) is unaffected.
        var afterWrap = OperatingSystem.IsWindows() ? GetMoveCursorLeft(3) : GetMoveCursorLeft(1);
        Assert.Equal(
            expected:
                ToAnsiEscapeSequenceSlow(format1) + "here\n" + afterWrap +
                Reset + " is \n" + afterWrap +
                ToAnsiEscapeSequenceSlow(format2) + "some\n" + afterWrap +
                Reset + " out\n" + afterWrap +
                "put" + GetMoveCursorLeft(3),
            actual: output
        );
    }
}
