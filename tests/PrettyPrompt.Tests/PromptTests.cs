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
    public static readonly string DefaultTabSpaces = new(' ', new PromptConfiguration().TabSize);

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

        // After "\n", Windows preserves the cursor column (DISABLE_NEWLINE_AUTO_RETURN) and moves left to the
        // wrapped line's start; other platforms return to column 1 (ONLCR) and move right to it.
        var moveToWrappedLine = OperatingSystem.IsWindows() ? GetMoveCursorLeft(2) : GetMoveCursorRight(2);
        Assert.Equal(
            expected: "111\n" + moveToWrappedLine +
                      "222\n" + moveToWrappedLine +
                      "333\n" + moveToWrappedLine,
            actual: finalOutput
        );
    }

    /// <summary>
    /// Regression test for https://github.com/waf/CSharpRepl/issues/356.
    /// When <see cref="PromptCallbacks.FormatInput"/> reformats the buffer on the very keystroke that
    /// submits the prompt, the submit render path must force a redraw so the committed line shows the
    /// formatted text. Otherwise the on-screen line is left showing the stale pre-format text, because
    /// the reformat happened after the previous render and the submit path skips redrawing by default.
    /// </summary>
    [Fact]
    public async Task ReadLine_FormatInputReformatsOnSubmit_RedrawsFormattedText()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"hello{Enter}");

        var prompt = new Prompt(
            callbacks: new TestPromptCallbacks
            {
                // Only reformat on the submit keystroke (Enter), so the formatted text has never been
                // rendered before submit - that's the scenario the fix addresses.
                FormatInputCallback = (text, caret, keyPress) =>
                    Task.FromResult(
                        keyPress.ConsoleKeyInfo.Key == Enter
                            ? (text.ToUpperInvariant(), caret)
                            : (text, caret))
            },
            console: console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);

        // The returned result reflects the formatted text (re-captured after AutoFormatDocument), not the
        // pre-format snapshot taken when the submit key was first handled.
        Assert.Equal("HELLO", result.Text);

        // ...and the final render repaints it; without the forced redraw the screen would still show the
        // lowercase "hello" typed before the submit keystroke.
        Assert.Contains("HELLO", console.GetFinalOutput());
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
    public async Task ReadLine_EmacsCharNavigation_CtrlBCtrlF()
    {
        // Ctrl+B / Ctrl+F are emacs aliases for Left/Right arrow
        var console = ConsoleStub.NewConsole();
        console.StubInput($"abcd{Control}{B}{Control}{B}X{Control}{F}Y{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        // "abcd": Ctrl+B twice puts the caret between 'b' and 'c' -> type X -> "abXcd";
        // Ctrl+F moves past 'c' -> type Y -> "abXcYd".
        Assert.Equal("abXcYd", result.Text);
    }

    [Fact]
    public async Task ReadLine_EmacsLineNavigation_CtrlPCtrlN()
    {
        // Ctrl+P / Ctrl+N are emacs aliases for Up/Down arrow
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"aaa{Shift}{Enter}",
            $"bbb{Shift}{Enter}",
            $"ccc",
            $"{Control}{P}{Control}{P}{Home}1",
            $"{Control}{N}{End}2",
            $"{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        // From line 3, Ctrl+P twice reaches line 1 (Home, type 1 -> "1aaa");
        // Ctrl+N moves down to line 2 (End, type 2 -> "bbb2").
        Assert.Equal($"1aaa{NewLine}bbb2{NewLine}ccc", result.Text);
    }

    [Fact]
    public async Task ReadLine_EmacsForwardDelete_CtrlD()
    {
        // Ctrl+D is the emacs alias for Delete
        var console = ConsoleStub.NewConsole();
        console.StubInput($"abcd{Home}{Control}{D}{Control}{D}{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        // Home moves to the start; Ctrl+D deletes 'a' then 'b'.
        Assert.Equal("cd", result.Text);
    }

    [Fact]
    public async Task ReadLine_DeleteToEndOfLine_CtrlK()
    {
        // Ctrl+K (emacs/readline kill-line) deletes from the caret to the end of the current line, not past the newline.
        // Even though it's technically a "cut" in emacs/readline terminology, that's to the kill ring,
        // so as a design choice we decided not to touch the system clipboard.
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText("");
            console.StubInput(
                $"aaXbb{Shift}{Enter}",
                $"ccc",
                $"{Control}{P}{Home}{RightArrow}{RightArrow}{Control}{K}{Enter}"
            );

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            // Caret after "aa" on line 1; Ctrl+K deletes "Xbb" (to end of line, leaving the newline intact).
            Assert.Equal($"aa{NewLine}ccc", result.Text);
            Assert.Equal("", console.Clipboard.GetText()); // clipboard left untouched
        }
    }

    [Fact]
    public async Task ReadLine_DeleteToStartOfLine_CtrlU()
    {
        // Ctrl+U (readline/bash unix-line-discard; in emacs Ctrl+U is universal-argument) deletes from
        // the start of the current line to the caret. Even though it's technically a "cut" in readline
        // terminology, that's to the kill ring, so as a design choice we decided not to touch the system clipboard.
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText("");
            console.StubInput($"hello world{Home}{RightArrow}{RightArrow}{RightArrow}{RightArrow}{RightArrow}{Control}{U}{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();

            // Caret after "hello"; Ctrl+U deletes "hello", leaving " world".
            Assert.Equal(" world", result.Text);
            Assert.Equal("", console.Clipboard.GetText()); // clipboard left untouched
        }
    }

    [Fact]
    public async Task ReadLine_LineKill_WithSelection_DeletesSelection()
    {
        // With an active selection, Ctrl+K / Ctrl+U delete the selection (like Backspace/Delete/Ctrl+D),
        // rather than killing to the line boundary or silently dropping the selection.
        var console = ConsoleStub.NewConsole();
        var prompt = new Prompt(console: console);

        // select "bc", then Ctrl+K deletes the selection -> "ad"
        console.StubInput($"abcd{Home}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Control}{K}{Enter}");
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ad", result.Text);

        // select "bc", then Ctrl+U deletes the selection -> "ad"
        console.StubInput($"abcd{Home}{RightArrow}{Shift}{RightArrow}{Shift}{RightArrow}{Control}{U}{Enter}");
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ad", result.Text);
    }

    [Fact]
    public async Task ReadLine_EmacsWordNavigation_AltFAltB()
    {
        // Alt+f / Alt+b are emacs aliases for Ctrl+RightArrow / Ctrl+LeftArrow (word motion).
        // Mirrors ReadLine_NextWordPrevWordKeys with the Alt keys so the same edits produce the same text.
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"aaaa bbbb 5555{Shift}{Enter}",
            $"dddd x5x5 foo.bar{Shift}{Enter}",
            $"{UpArrow}{Alt}{F}{Alt}{F}{Alt}{F}{Alt}{F}lum",
            $"{Alt}{B}{Alt}{B}{Alt}{B}{Backspace}{Tab}",
            $"{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal($"aaaa bbbb 5555{NewLine}dddd x5x5{DefaultTabSpaces}foo.lumbar{NewLine}", result.Text);
    }

    [Fact]
    public async Task ReadLine_EmacsDeleteWord_AltDAltBackspace()
    {
        // Alt+d / Alt+Backspace are emacs aliases for Ctrl+Delete / Ctrl+Backspace (word delete).
        // Mirrors ReadLine_DeleteWordPrevWordKeys with the Alt keys.
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"aaaa bbbb cccc{Shift}{Enter}",
            $"dddd eeee ffff{Shift}{Enter}",
            $"{UpArrow}{Alt}{D}{Alt}{Backspace}",
            $"{Enter}"
        );

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        Assert.Equal($"aaaa bbbb eeee ffff{NewLine}", result.Text);
    }

    [Fact]
    public async Task ReadLine_DeleteWordForward_AltDelete()
    {
        // Alt+Delete = delete word forward (Mac Option+forward-delete), an alias of Ctrl+Delete.
        var console = ConsoleStub.NewConsole();
        console.StubInput($"foo bar baz{Home}{Alt}{Delete}{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        // Home moves to start; Alt+Delete deletes the first word forward (including its trailing space).
        Assert.Equal("bar baz", result.Text);
    }

    [Fact]
    public async Task ReadLine_CtrlH_DeletesWordBackward()
    {
        // Ctrl+H is bound to delete-word-backward, a true alias of Ctrl+Backspace (issue #277).
        // On macOS the Ctrl+H byte (0x08) is reported by .NET as (Control, Backspace) anyway, so it
        // lands on the same action; on Windows it arrives as (Control, H). Both resolve to delete-word.
        var console = ConsoleStub.NewConsole();
        console.StubInput($"foo bar baz{Control}{H}{Enter}");

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();

        // Ctrl+H deletes the last word "baz".
        Assert.Equal("foo bar ", result.Text);
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
    public async Task ReadLine_Paste_TextWithNoLeadingIndentationPreservesNewlines()
    {
        var console = ConsoleStub.NewConsole();

        console.KeyAvailable
            .Returns(true, $"indent\r    more indent\r\rinden".Select(_ => true).Append(false).ToArray());
        console.StubInput($"indent\r    more indent\r\rindent{Enter}");

        var prompt = new Prompt(console: console);

        var result = await prompt.ReadLineAsync();

        Assert.Equal($"indent{NewLine}    more indent{NewLine}{NewLine}indent", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/168
    /// </summary>
    [Fact]
    public async Task ReadLine_Paste_DoesNotTrimLeadingIndentation()
    {
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.Clipboard.SetText("    abcd");
            console.StubInput($"{Control}{V}{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.Equal($"    abcd", result.Text);

            ///////////////////

            console.Clipboard.SetText("    abcd\n");
            console.StubInput($"{Control}{V}{Enter}");
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.Equal($"    abcd{NewLine}", result.Text);

            ///////////////////

            console.Clipboard.SetText("\n    abcd\n");
            console.StubInput($"{Control}{V}{Enter}");
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.Equal($"{NewLine}    abcd{NewLine}", result.Text);

            ///////////////////

            console.Clipboard.SetText("\n    abcd\t\n");
            console.StubInput($"{Control}{V}{Enter}");
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.Equal($"{NewLine}    abcd{DefaultTabSpaces}{NewLine}", result.Text);
        }
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
            // the prompt normalizes line endings to the environment's newline ("\r\n" on Windows, "\n" elsewhere).
            Assert.Equal(Text.ReplaceLineEndings(), result.Text);
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
    /// https://github.com/waf/PrettyPrompt/issues/270
    /// Pasting must preserve the zero-width joiners, emoji modifiers, and variation selectors that
    /// hold a grapheme cluster together - they must not be filtered out as "zero-width" characters,
    /// otherwise the cluster breaks apart (e.g. 🤦🏼‍♂️ would render as 🤦🏼 followed by ♂).
    /// </summary>
    [Fact]
    public async Task ReadLine_PasteEmojiAndCombiningSequences_ArePreservedIntact()
    {
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            const string FacePalm = "\U0001F926\U0001F3FC\u200D\u2642\uFE0F"; // 🤦🏼‍♂️ (ZWJ sequence with VS-16)
            console.Clipboard.SetText(FacePalm);
            console.StubInput($"{Control}{V}{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(FacePalm, result.Text);

            ////////////////////////////////////////////////

            const string Accented = "e\u0301"; // é = base letter + combining acute accent
            console.Clipboard.SetText(Accented);
            console.StubInput($"{Control}{V}{Enter}");
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(Accented, result.Text);
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

    /// <summary>
    /// Triggers bug from https://github.com/waf/PrettyPrompt/issues/254.
    /// </summary>
    [Fact]
    public async Task ReadLine_DeleteLineFromMultipleEmptyLines_DeletesLine()
    {
        var console = ConsoleStub.NewConsole();
        var input = new List<FormattableString>
        {
            $"{Shift}{Enter}",
            $"{Shift}{Enter}",
            $"{UpArrow}{UpArrow}",
            $"{Shift}{Delete}",
            $"{Enter}"
        };
        console.StubInput(input.ToArray());
        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.Equal(NewLine, result.Text);
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

    [Fact] // https://github.com/waf/CSharpRepl/issues/260
    public async Task ReadLine_NonQwertyKeyboardLayout_CurlyBraceCanBeTyped()
    {
        var console = ConsoleStub.NewConsole();
        // on azerty keyboards, AltGr-4 (or ctrl-alt-4) is used to type curly brace.
        console.StubInput(new List<ConsoleKeyInfo>
        {
            new ConsoleKeyInfo('{', D4, shift: false, alt: true, control: true),
            new ConsoleKeyInfo('\0', Enter, shift: false, alt: false, control: false)
        });

        var prompt = new Prompt(console: console);
        var result = await prompt.ReadLineAsync();
        Assert.Equal("{", result.Text);
    }

    [Fact] // https://github.com/waf/PrettyPrompt/pull/252
    public async Task ReadLine_KeyBindingUsesKey_KeyBindingDoesNotInsertKey()
    {
        // set up a keybinding with ctrl-alt-space, and then fire that keybinding.
        // space should not be typed, and the keybinding output should be inserted.
        var console = ConsoleStub.NewConsole();
        console.StubInput(new List<ConsoleKeyInfo>
        {
            new ConsoleKeyInfo(' ', Spacebar, shift: false, alt: true, control: true),
            new ConsoleKeyInfo('\0', Enter, shift: false, alt: false, control: false)
        });

        var prompt = new Prompt(
            console: console,
            callbacks: new TestPromptCallbacks(
                (
                    new KeyPressPattern(Control | Alt, Spacebar),
                    (_, _, _) => Task.FromResult<KeyPressCallbackResult?>(new KeyPressCallbackResult("my-keybinding-output", null))
                )
            )
        );
        var result = await prompt.ReadLineAsync();
        Assert.Equal("my-keybinding-output", result.Text);
    }

    [Fact]
    public async Task ReadLine_StreamingInputCallbackReturnsOutput_IsReturned()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Hello World{Control}{LeftArrow}{Control}{Alt}{Spacebar}{Enter}");

        var callbackOutput = new StreamingInputCallbackResult(ResultAsync());
        var prompt = new Prompt(
            callbacks: new TestPromptCallbacks(
            (
                new KeyPressPattern(Control | Alt, Spacebar),
                (inputArg, caretArg, _) =>
                {
                    return Task.FromResult<KeyPressCallbackResult?>(callbackOutput);
                }
            )),
            console: console
        );

        var result = await prompt.ReadLineAsync();
        Assert.Equal("Hello Asynchronous Streaming World", result.Text);

        static async IAsyncEnumerable<string> ResultAsync()
        {
            await Task.Yield();
            yield return "Asynchronous ";
            yield return "Streaming ";
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/235
    /// </summary>
    [Fact]
    public async Task ReadLineAsync_PromptModification()
    {
        var console = ConsoleStub.NewConsole();
        var cfg = new PromptConfiguration();

        console.StubInput(
            ConsoleStub.Input($"A{Enter}", () => cfg.Prompt = "***"),
            ConsoleStub.Input($"B{Enter}"));

        var prompt = new Prompt(console: console, configuration: cfg);

        var result = await prompt.ReadLineAsync();
        Assert.Equal("A", result.Text);
        result = await prompt.ReadLineAsync();
        Assert.Equal("B", result.Text);

        var output = console.GetAllOutput();
        Assert.Equal("> ", output[1]);
        Assert.Equal("***", output[5]);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/270
    /// Arrow keys, Backspace, and Delete must operate on whole grapheme clusters, not single UTF-16
    /// code units, so editing around an emoji never splits its surrogate pair / joiners.
    /// </summary>
    [Fact]
    public async Task ReadLine_EditAroundEmoji_TreatsClusterAsOneUnit()
    {
        const string Emoji = "\U0001F926\U0001F3FC\u200D\u2642\uFE0F"; // 🤦🏼‍♂️
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            // Backspace removes the whole cluster (caret is at the end after pasting), then 'b'.
            console.Clipboard.SetText("a" + Emoji + "b");
            console.StubInput($"{Control}{V}{Backspace}{Backspace}{Enter}");
            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync();
            Assert.Equal("a", result.Text);

            // Left arrow steps over the whole cluster: end -> before 'b' -> before the emoji, then insert.
            console.Clipboard.SetText("a" + Emoji + "b");
            console.StubInput($"{Control}{V}{LeftArrow}{LeftArrow}x{Enter}");
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.Equal("ax" + Emoji + "b", result.Text);

            // Delete (forward) removes the whole cluster: Home, delete 'a', delete the emoji.
            console.Clipboard.SetText("a" + Emoji + "b");
            console.StubInput($"{Control}{V}{Home}{Delete}{Delete}{Enter}");
            prompt = new Prompt(console: console);
            result = await prompt.ReadLineAsync();
            Assert.Equal("b", result.Text);
        }
    }
}