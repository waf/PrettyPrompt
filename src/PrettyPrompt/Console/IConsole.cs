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
    int CursorTop { get; }
    int BufferWidth { get; }
    int WindowHeight { get; }
    int WindowTop { get; }

    void Write(string? value);
    void WriteLine(string? value);
    void WriteError(string? value);
    void WriteErrorLine(string? value);

    void Write(ReadOnlySpan<char> value);
    void WriteLine(ReadOnlySpan<char> value);
    void WriteError(ReadOnlySpan<char> value);
    void WriteErrorLine(ReadOnlySpan<char> value);

    void Clear();
    void ShowCursor();
    void HideCursor();
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey(bool intercept);

    /// <summary>
    /// Enables ANSI escape codes for controlling the terminal.
    /// https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences
    /// </summary>
    void InitVirtualTerminalProcessing();

    event ConsoleCancelEventHandler CancelKeyPress;
    bool CaptureControlC { get; set; }

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
    {
        if (!PromptConfiguration.HasUserOptedOutFromColor &&
            value.FormatSpans.Length > 0)
        {
            var lastFormatting = ConsoleFormat.None;
            console.Write(AnsiEscapeCodes.Reset);
            foreach (var (element, formatting) in value.EnumerateTextElements())
            {
                if (!lastFormatting.Equals(in formatting))
                {
                    console.Write(AnsiEscapeCodes.Reset);
                    console.Write(AnsiEscapeCodes.ToAnsiEscapeSequenceSlow(formatting).ToString());
                    lastFormatting = formatting;
                }
                console.Write(element.ToString());
            }
            console.Write(AnsiEscapeCodes.Reset);
        }
        else
        {
            console.Write(value.Text);
        }
    }

    public static void WriteLine(this IConsole console, FormattedString value)
    {
        console.Write(value);
        console.WriteLine("");
    }
}

internal interface IConsoleWithClipboard : IConsole
{
    IClipboard Clipboard { get; }
    IDisposable ProtectClipboard();
}