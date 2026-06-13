#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Text;
using PrettyPrompt.Highlighting;
using TextCopy;

namespace PrettyPrompt.Consoles;

/// <summary>
/// Console abstraction, mainly for testability.
/// In the real application it will be the System.Console APIs.
/// </summary>
public interface IConsole
{
    /// <inheritdoc cref="Console.CursorTop"/>
    int CursorTop { get; }

    /// <inheritdoc cref="Console.BufferWidth"/>
    int BufferWidth { get; }

    /// <inheritdoc cref="Console.WindowHeight"/>
    int WindowHeight { get; }

    /// <inheritdoc cref="Console.WindowTop"/>
    int WindowTop { get; }

    /// <inheritdoc cref="Console.KeyAvailable"/>
    bool KeyAvailable { get; }

    /// <inheritdoc cref="Console.IsErrorRedirected"/>
    bool IsErrorRedirected { get; }

    bool CaptureControlC { get; set; }

    void Write(string? value);
    void WriteLine(string? value);
    void WriteError(string? value);
    void WriteErrorLine(string? value);

    void Write(ReadOnlySpan<char> value);
    void WriteLine(ReadOnlySpan<char> value);
    void WriteError(ReadOnlySpan<char> value);
    void WriteErrorLine(ReadOnlySpan<char> value);

    /// <inheritdoc cref="Console.Clear"/>
    void Clear();

    void ShowCursor();
    void HideCursor();

    /// <inheritdoc cref="Console.ReadKey(bool)"/>
    ConsoleKeyInfo ReadKey(bool intercept);

    /// <summary>
    /// Enables ANSI escape codes for controlling the terminal.
    /// https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences
    /// </summary>
    void InitVirtualTerminalProcessing();

    /// <summary>
    /// Controls whether writing a line feed ("\n") also returns the cursor to the first column.
    /// The prompt disables auto-return while it renders (the renderer positions the cursor with explicit
    /// escape codes and requires "\n" to be a pure line feed), and re-enables it when
    /// <see cref="Prompt.ReadLineAsync"/> returns, so output written by the host application between
    /// prompts behaves normally. Only meaningful on Windows (the DISABLE_NEWLINE_AUTO_RETURN console mode
    /// flag); terminals on other platforms translate "\n" to "\r\n" themselves via the ONLCR output flag.
    /// </summary>
    void SetNewlineAutoReturn(bool enabled) { }

    /// <summary>
    /// Enables xterm "modifyOtherKeys" mode for the duration of the prompt, so that key combinations
    /// that normally share an encoding with an unmodified key (Shift/Ctrl/Alt+Enter all otherwise arrive
    /// as a bare CR, indistinguishable from plain Enter) are reported as distinct CSI 27 escape sequences
    /// that <see cref="KeyPress"/> can parse. The prompt enables this on entry and resets it when
    /// <see cref="Prompt.ReadLineAsync"/> returns. This is only relevant on Unix/macOS, where .NET
    /// reconstructs keys from the terminal byte stream; on Windows the key events already carry modifiers.
    /// No-op for non-system consoles (e.g. unit-test stubs).
    /// </summary>
    void SetModifyOtherKeys(bool enabled) { }

    event ConsoleCancelEventHandler CancelKeyPress;

    #region Write StringBuilder default implementations
    //This could be extension methods, but we need to override them in unit tests because
    //we want to have StringBuilder writes in single Write call (we are checking result via NSubsitute).
    //If we would not override them results of test would be non-deterministic because
    //we do not have control over chunking policy of StringBuilder.

    /// <inheritdoc cref="IConsoleX.Write(IConsole, ReadOnlySpan{char}, bool)"/>
    void Write(StringBuilder value, bool hideCursor = false)
    {
        if (hideCursor) HideCursor();
        foreach (var chunkMemory in value.GetChunks()) Write(chunkMemory.Span);
        if (hideCursor) ShowCursor();
    }

    /// <inheritdoc cref="IConsoleX.Write(IConsole, ReadOnlySpan{char}, bool)"/>
    void WriteLine(StringBuilder value, bool hideCursor = false)
    {
        if (hideCursor) HideCursor();
        foreach (var chunkMemory in value.GetChunks()) WriteLine(chunkMemory.Span);
        if (hideCursor) ShowCursor();
    }

    /// <inheritdoc cref="IConsoleX.Write(IConsole, ReadOnlySpan{char}, bool)"/>
    void WriteError(StringBuilder value, bool hideCursor = false)
    {
        if (hideCursor) HideCursor();
        foreach (var chunkMemory in value.GetChunks()) WriteError(chunkMemory.Span);
        if (hideCursor) ShowCursor();
    }

    /// <inheritdoc cref="IConsoleX.Write(IConsole, ReadOnlySpan{char}, bool)"/>
    void WriteErrorLine(StringBuilder value, bool hideCursor = false)
    {
        if (hideCursor) HideCursor();
        foreach (var chunkMemory in value.GetChunks()) WriteErrorLine(chunkMemory.Span);
        if (hideCursor) ShowCursor();
    }
    #endregion
}

public static class IConsoleX
{
    /// <param name="console">Console.</param>
    /// <param name="value">Value to be written to console.</param>
    /// <param name="hideCursor">HideCursor() is surprisingly slow, don't use it unless we're rendering something large. The issue mainly shows when e.g. repeating characters by holding down a key (e.g. spacebar),</param>
    public static void Write(this IConsole console, ReadOnlySpan<char> value, bool hideCursor)
    {
        if (hideCursor) console.HideCursor();
        console.Write(value);
        if (hideCursor) console.ShowCursor();
    }

    /// <inheritdoc cref="Write(IConsole, ReadOnlySpan{char}, bool)"/>
    public static void WriteLine(this IConsole console, ReadOnlySpan<char> value, bool hideCursor)
    {
        if (hideCursor) console.HideCursor();
        console.WriteLine(value);
        if (hideCursor) console.ShowCursor();
    }

    /// <inheritdoc cref="Write(IConsole, ReadOnlySpan{char}, bool)"/>
    public static void WriteError(this IConsole console, ReadOnlySpan<char> value, bool hideCursor)
    {
        if (hideCursor) console.HideCursor();
        console.WriteError(value);
        if (hideCursor) console.ShowCursor();
    }

    /// <inheritdoc cref="Write(IConsole, ReadOnlySpan{char}, bool)"/>
    public static void WriteErrorLine(this IConsole console, ReadOnlySpan<char> value, bool hideCursor)
    {
        if (hideCursor) console.HideCursor();
        console.WriteErrorLine(value);
        if (hideCursor) console.ShowCursor();
    }

    public static void Write(this IConsole console, FormattedString value)
        => Write(value, console.Write);

    public static void WriteError(this IConsole console, FormattedString value)
    {
        if (console.IsErrorRedirected)
        {
            //when error stream is redirected we can ignore value formatting
            console.WriteError(value.Text);
        }
        else
        {
            Write(value, console.WriteError);
        }
    }

    private static void Write(FormattedString value, Action<string?> write)
    {
        if (!PromptConfiguration.HasUserOptedOutFromColor &&
            value.FormatSpans.Length > 0)
        {
            var lastFormatting = ConsoleFormat.None;
            write(AnsiEscapeCodes.Reset);
            foreach (var (element, formatting) in value.EnumerateTextElements())
            {
                if (!lastFormatting.Equals(in formatting))
                {
                    write(AnsiEscapeCodes.Reset);
                    write(AnsiEscapeCodes.ToAnsiEscapeSequenceSlow(formatting).ToString());
                    lastFormatting = formatting;
                }
                write(element.ToString());
            }
            write(AnsiEscapeCodes.Reset);
        }
        else
        {
            write(value.Text);
        }
    }

    public static void WriteLine(this IConsole console, FormattedString value)
    {
        console.Write(value);
        console.WriteLine("");
    }

    public static void WriteErrorLine(this IConsole console, FormattedString value)
    {
        console.WriteError(value);
        console.WriteLine("");
    }
}

internal interface IConsoleWithClipboard : IConsole
{
    IClipboard Clipboard { get; }
    IDisposable ProtectClipboard();
}