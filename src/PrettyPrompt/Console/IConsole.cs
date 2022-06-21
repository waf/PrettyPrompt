#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
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
}

public static class IConsoleX
{
    public static void Write(this IConsole console, FormattedString value)
    {
        if (!PromptConfiguration.HasUserOptedOutFromColor &&
            value.FormatSpans.Count > 0)
        {
            var lastFormatting = ConsoleFormat.None;
            console.Write(AnsiEscapeCodes.Reset);
            foreach (var (element, formatting) in value.EnumerateTextElements())
            {
                if (!lastFormatting.Equals(in formatting))
                {
                    console.Write(AnsiEscapeCodes.Reset);
                    console.Write(AnsiEscapeCodes.ToAnsiEscapeSequence(formatting));
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