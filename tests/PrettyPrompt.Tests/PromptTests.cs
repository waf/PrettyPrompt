#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static System.Environment;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;

namespace PrettyPrompt.Tests;

public class PromptTests
{
    private static readonly string DefaultTabSpaces = new(' ', new PromptConfiguration().TabSize);

    [Fact]
    public async Task ReadLine_TypeSimpleString_GetSimpleString()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Hello World{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello World", result.Text);
    }

    [Fact]
    public async Task ReadLine_ControlEnter_IsHardEnter()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Hello World{Control}{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(Control, result.SubmitKeyInfo.Modifiers);
        Assert.Equal(Enter, result.SubmitKeyInfo.Key);
        Assert.Equal("Hello World", result.Text);
    }

    [Fact]
    public async Task ReadLine_WhitespacePrompt_ReturnsWhitespace()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"  {Enter}");

        // add completion handler, as it has caused problem when completing all whitespace prompts
        var prompt = new Prompt(
            callbacks: new TestPromptCallbacks
            {
                CompletionCallback = new CompletionTestData().CompletionHandlerAsync
            },
            console: console
        );

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("  ", result.Text);
    }

    [Fact]
    public async Task ReadLine_Abort_NoResult()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Hello World{Control}c");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Text);
    }

    [Fact]
    public async Task ReadLine_WordWrap()
    {
        // window width of 5, with a 2 char prompt.
        var console = ConsoleStub.NewConsole(width: 5);
        console.StubInput($"111222333{Control}{L}{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("111222333", result.Text);

        var finalOutput = console.GetFinalOutput();

        Assert.Equal(
            expected: "111\n" + MoveCursorLeft(2) +
                      "222\n" + MoveCursorLeft(2) +
                      "333\n" + MoveCursorLeft(2),
            actual: finalOutput
        );
    }

    [Fact]
    public async Task ReadLine_HorizontalNavigationKeys()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"pretty{Backspace}{Backspace}{Home}{LeftArrow}{RightArrow}{RightArrow}{Delete}omp{RightArrow}{Home}{End}!{RightArrow}{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal("prompt!", result.Text);
    }

    [Fact]
    public async Task ReadLine_HomeEndKeys_NavigateLines()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"{Shift}{Enter}{Shift}{Enter}hello{Home}{Delete}H{End}!{Shift}{Enter}",
            $"world{Control}{Home}I say:{Control}{End}!{Home}{Delete}W{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal($"I say:{NewLine}{NewLine}Hello!{NewLine}World!", result.Text);
    }

    [Fact]
    public async Task ReadLine_VerticalNavigationKeys()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"pretty{Shift}{Enter}",
            $"unit-tested{Shift}{Enter}",
            $"prompt",
            $"{UpArrow}{RightArrow}{RightArrow}{RightArrow}{RightArrow}{RightArrow}?",
            $"{UpArrow} well",
            $"{DownArrow}{DownArrow}!{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal($"pretty well{NewLine}unit-tested?{NewLine}prompt!", result.Text);
    }

    /// <summary>
    /// Triggered bug from https://github.com/waf/PrettyPrompt/issues/160.
    /// </summary>
    [Fact]
    public async Task ReadLine_ArrowDownToLastEmptyLine()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"abc{Shift}{Enter}{LeftArrow}{DownArrow}x{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"abc{NewLine}x", result.Text);
    }

    [Fact]
    public async Task ReadLine_NextWordPrevWordKeys()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"aaaa bbbb 5555{Shift}{Enter}",
            $"dddd x5x5 foo.bar{Shift}{Enter}",
            $"{UpArrow}{Control}{RightArrow}{Control}{RightArrow}{Control}{RightArrow}{Control}{RightArrow}lum",
            $"{Control}{LeftArrow}{Control}{LeftArrow}{Control}{LeftArrow}{Backspace}{Tab}",
            $"{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal($"aaaa bbbb 5555{NewLine}dddd x5x5{DefaultTabSpaces}foo.lumbar{NewLine}", result.Text);
    }

    [Fact]
    public async Task ReadLine_DeleteWordPrevWordKeys()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"aaaa bbbb cccc{Shift}{Enter}",
            $"dddd eeee ffff{Shift}{Enter}",
            $"{UpArrow}{Control}{Delete}{Control}{Backspace}",
            $"{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal($"aaaa bbbb eeee ffff{NewLine}", result.Text);
    }

    [Fact]
    public async Task ReadLine_TypeReallyQuickly_DoesNotDropKeyPresses()
    {
        var console = ConsoleStub.NewConsole();
        // it's possible that if keys are pressed simultaneously / quickly, we'll still have
        // some keys in the buffer after calling Console.ReadKey()
        console.KeyAvailable.Returns(true, true, false);
        console.StubInput($"abc{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal($"abc", result.Text);
    }

    [Fact]
    public async Task ReadLine_Paste_DoesNotRepeatedlySyntaxHighlight()
    {
        var console = ConsoleStub.NewConsole();
        console.KeyAvailable.Returns(true, " am pasting conten".Select(_ => true).Append(false).ToArray());
        console.StubInput($"I am pasting content{LeftArrow}{RightArrow}{Enter}");

        int syntaxHighlightingInvocations = 0;

        var prompt = new Prompt(callbacks: new TestPromptCallbacks
        {
            HighlightCallback = text =>
            {
                syntaxHighlightingInvocations++;
                return Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>());
            }
        }, console: console);

        var result = await prompt.ReadLineAsync();

        Assert.Equal("I am pasting content", result.Text);
        Assert.Equal(1, syntaxHighlightingInvocations);
    }

    [Fact]
    public async Task ReadLine_Paste_TrimsLeadingIndentation()
    {
        var console = ConsoleStub.NewConsole();

        console.KeyAvailable
            .Returns(true, $"   indent\r        more indent\r\r    inden".Select(_ => true).Append(false).ToArray());
        console.StubInput($"    indent\r        more indent\r\r    indent{Enter}");

        var prompt = new Prompt(console: console);

        var result = await prompt.ReadLineAsync();

        Assert.Equal($"indent{NewLine}    more indent{NewLine}{NewLine}indent", result.Text);
    }

    [Fact]
    public async Task ReadLine_KeyPressCallback_IsInvoked()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"I like apple{Control}{LeftArrow}{Control}{LeftArrow}{F1}{Enter}");

        string? input = null;
        int? caret = null;
        var prompt = new Prompt(callbacks: new TestPromptCallbacks(
            (
                new KeyPressPattern(F1),
                (inputArg, caretArg, _) => { input = inputArg; caret = caretArg; return Task.FromResult<KeyPressCallbackResult?>(null); }
        )),
            console: console);

        _ = await prompt.ReadLineAsync();

        Assert.Equal("I like apple", input);
        Assert.Equal(2, caret);
    }

    /// <summary>
    /// Triggered issue: https://github.com/waf/PrettyPrompt/issues/63
    /// </summary>
    [Fact]
    public async Task ReadLine_PasteMultipleLines()
    {
        const string Text = "abc\r\ndef";
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText(Text);
            console.StubInput($"{Control}{V}{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(Text, result.Text);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/55
    /// https://github.com/waf/PrettyPrompt/issues/166
    /// </summary>
    [Fact]
    public async Task ReadLine_PasteTabWithChar()
    {
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            //#55
            console.Clipboard.SetText("\ta");
            console.StubInput($"{Control}{V}{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal($"{DefaultTabSpaces}a", result.Text);

            ////////////////////////////////////////////////

            //#166
            console.Clipboard.SetText("\r\n\r\n");
            console.StubInput($"{Control}{V}{Enter}");
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal($"{NewLine}{NewLine}", result.Text);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/151
    /// </summary>
    [Fact]
    public async Task ReadLine_CutLine()
    {
        await TestLineCutting(cutKeyPress: $"{Control}{X}");
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/152
    /// </summary>
    [Fact]
    public async Task ReadLine_DeleteLine()
    {
        await TestLineCutting(cutKeyPress: $"{Shift}{Delete}");
    }

    private static async Task TestLineCutting(FormattableString cutKeyPress)
    {
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText("");

            //cutting single line
            for (int count = 0; count < 5; count++)
            {
                var text = new string('a', count);
                var input = new List<FormattableString>();
                input.Add($"{text}");
                input.Add(cutKeyPress);
                input.Add($"z{Enter}");
                console.StubInput(input.ToArray());
                var prompt = new Prompt(console: console);
                var result = await prompt.ReadLineAsync();
                Assert.True(result.IsSuccess);
                Assert.Equal($"z", result.Text);
                if (cutKeyPress.GetArgument(1) is X)
                {
                    Assert.Equal(text, console.Clipboard.GetText());
                }
                else
                {
                    Assert.Equal("", console.Clipboard.GetText());
                }
            }

            //////////////////////////////////////////////

            //cutting empty line from multiple empty lines
            for (int lineCount = 2; lineCount < 6; lineCount++)
            {
                for (int upArrowCount = 0; upArrowCount < lineCount; upArrowCount++)
                {
                    var input = new List<FormattableString>();
                    input.AddRange(Enumerable.Repeat<FormattableString>($"{Shift}{Enter}", lineCount));
                    input.AddRange(Enumerable.Repeat<FormattableString>($"{UpArrow}", upArrowCount));
                    input.Add(cutKeyPress);
                    input.Add($"{Enter}");
                    console.StubInput(input.ToArray());
                    var prompt = new Prompt(console: console);
                    var result = await prompt.ReadLineAsync();
                    Assert.True(result.IsSuccess);
                    if (upArrowCount == 0)
                    {
                        Assert.Equal(Enumerable.Repeat(NewLine, lineCount).Aggregate((a, b) => a + b), result.Text);
                    }
                    else
                    {
                        Assert.Equal(Enumerable.Repeat(NewLine, lineCount - 1).Aggregate((a, b) => a + b), result.Text);
                        if (cutKeyPress.GetArgument(1) is X)
                        {
                            Assert.Equal("\n", console.Clipboard.GetText());
                        }
                        else
                        {
                            Assert.Equal("", console.Clipboard.GetText());
                        }
                    }
                }
            }

            //////////////////////////////////////////////

            //cutting line from multiple lines
            for (int lineCount = 2; lineCount < 6; lineCount++)
            {
                for (int upArrowCount = 0; upArrowCount < lineCount; upArrowCount++)
                {
                    var input = new List<FormattableString>();
                    var outputLines = new List<string>();
                    for (int i = 0; i < lineCount; i++)
                    {
                        if (i % 2 == 0)
                        {
                            input.Add($"{Shift}{Enter}");
                            outputLines.Add(NewLine);
                        }
                        else
                        {
                            input.Add($"abcdef{Shift}{Enter}");
                            outputLines.Add("abcdef" + NewLine);
                        }
                    }

                    string clipboardOutput = "";
                    if (upArrowCount > 0)
                    {
                        clipboardOutput = outputLines[lineCount - upArrowCount];
                        outputLines.RemoveAt(lineCount - upArrowCount);
                    }
                    var output = outputLines.Aggregate((a, b) => a + b);

                    input.AddRange(Enumerable.Repeat<FormattableString>($"{UpArrow}", upArrowCount));
                    input.Add(cutKeyPress);
                    input.Add($"{Enter}");
                    console.StubInput(input.ToArray());
                    var prompt = new Prompt(console: console);
                    var result = await prompt.ReadLineAsync();
                    Assert.True(result.IsSuccess);
                    Assert.Equal(output, result.Text);
                    if (upArrowCount > 0)
                    {
                        if (cutKeyPress.GetArgument(1) is X)
                        {
                            Assert.Equal(clipboardOutput.Replace("\r\n", "\n"), console.Clipboard.GetText());
                        }
                        else
                        {
                            Assert.Equal("", console.Clipboard.GetText());
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task ReadLine_KeyPressCallbackReturnsOutput_IsReturned()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"I like apple{Control}{LeftArrow}{Control}{LeftArrow}{F2}{Enter}");

        var callbackOutput = new KeyPressCallbackResult("", "Callback output!");
        var prompt = new Prompt(callbacks: new TestPromptCallbacks(
            (
                new KeyPressPattern(F2),
                (inputArg, caretArg, _) =>
                {
                    return Task.FromResult<KeyPressCallbackResult?>(callbackOutput);
                }
        )),
            console: console);

        var result = await prompt.ReadLineAsync();
        Assert.Equal(callbackOutput, result);
    }
}