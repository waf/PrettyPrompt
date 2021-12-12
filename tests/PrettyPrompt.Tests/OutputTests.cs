﻿using System;
using PrettyPrompt.Highlighting;
using Xunit;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;

namespace PrettyPrompt.Tests;

public class OutputTests
{
    [Fact]
    public void RenderAnsiOutput_PlainText()
    {
        var output = Prompt.RenderAnsiOutput("here is some output", Array.Empty<FormatSpan>(), 100);

        Assert.Equal("here is some output" + MoveCursorLeft(19), output);
    }

    [Fact]
    public void RenderAnsiOutput_GivenFormat_AppliesAnsiEscapeSequences()
    {
        var output = Prompt.RenderAnsiOutput("here is some output", new[]
        {
                new FormatSpan(0, 4, new ConsoleFormat { Foreground = AnsiColor.Red.Foreground }),
                new FormatSpan(8, 4, new ConsoleFormat { Foreground = AnsiColor.Green.Foreground }),
            }, 100);

        Assert.Equal(
            Red.Foreground + "here" + Reset + " is " + Green.Foreground + "some" + Reset + " output" + MoveCursorLeft(19),
            output
        );
    }

    [Fact]
    public void RenderAnsiOutput_GivenFormatAndWrapping_AppliesAnsiEscapeSequences()
    {
        var output = Prompt.RenderAnsiOutput("here is some output", new[]
        {
                new FormatSpan(0, 4, new ConsoleFormat { Foreground = AnsiColor.Red.Foreground }),
                new FormatSpan(8, 4, new ConsoleFormat { Foreground = AnsiColor.Green.Foreground }),
            }, 4);

        Assert.Equal(
            expected:
                Red.Foreground + "here\n" + MoveCursorLeft(3) +
                Reset + " is \n" + MoveCursorLeft(3) +
                Green.Foreground + "some\n" + MoveCursorLeft(3) +
                Reset + " out\n" + MoveCursorLeft(3) +
                "put" + MoveCursorLeft(3),
            actual: output
        );
    }
}
