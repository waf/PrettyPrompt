﻿#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Linq;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Consoles;

public static class AnsiEscapeCodes
{
    private const char Escape = '\u001b';
    private const string ResetForegroundColor = "39";
    private const string ResetBackgroundColor = "49";
    private const string Bold = "1";
    private const string Underline = "4";
    private const string Reverse = "7";
    public static readonly string ClearLine = $"{Escape}[0K";
    public static readonly string ClearToEndOfScreen = $"{Escape}[0J";
    public static readonly string ClearEntireScreen = $"{Escape}[2J";

    /// <summary>
    /// index starts at 1!
    /// </summary>
    public static string MoveCursorToColumn(int index) => $"{Escape}[{index}G";

    public static string MoveCursorUp(int count) => count == 0 ? "" : $"{Escape}[{count}A";
    public static string MoveCursorDown(int count) => count == 0 ? "" : $"{Escape}[{count}B";
    public static string MoveCursorRight(int count) => count == 0 ? "" : $"{Escape}[{count}C";
    public static string MoveCursorLeft(int count) => count == 0 ? "" : $"{Escape}[{count}D";

    public static string ForegroundColor(byte r, byte g, byte b) => ToAnsiEscapeSequence(new ConsoleFormat(Foreground: AnsiColor.ForegroundRgb(r, g, b)));
    public static string BackgroundColor(byte r, byte g, byte b) => ToAnsiEscapeSequence(new ConsoleFormat(Background: AnsiColor.BackgroundRgb(r, g, b)));

    public static readonly PredefinedAnsiEscapeCode Black = new(AnsiColor.Black);
    public static readonly PredefinedAnsiEscapeCode Red = new(AnsiColor.Red);
    public static readonly PredefinedAnsiEscapeCode Green = new(AnsiColor.Green);
    public static readonly PredefinedAnsiEscapeCode Yellow = new(AnsiColor.Yellow);
    public static readonly PredefinedAnsiEscapeCode Blue = new(AnsiColor.Blue);
    public static readonly PredefinedAnsiEscapeCode Magenta = new(AnsiColor.Magenta);
    public static readonly PredefinedAnsiEscapeCode Cyan = new(AnsiColor.Cyan);
    public static readonly PredefinedAnsiEscapeCode White = new(AnsiColor.White);
    public static readonly PredefinedAnsiEscapeCode BrightBlack = new(AnsiColor.BrightBlack);
    public static readonly PredefinedAnsiEscapeCode BrightRed = new(AnsiColor.BrightRed);
    public static readonly PredefinedAnsiEscapeCode BrightGreen = new(AnsiColor.BrightGreen);
    public static readonly PredefinedAnsiEscapeCode BrightYellow = new(AnsiColor.BrightYellow);
    public static readonly PredefinedAnsiEscapeCode BrightBlue = new(AnsiColor.BrightBlue);
    public static readonly PredefinedAnsiEscapeCode BrightMagenta = new(AnsiColor.BrightMagenta);
    public static readonly PredefinedAnsiEscapeCode BrightCyan = new(AnsiColor.BrightCyan);
    public static readonly PredefinedAnsiEscapeCode BrightWhite = new(AnsiColor.BrightWhite);

    public static readonly string Reset = $"{Escape}[0m";

    public static string SetColors(AnsiColor fg, AnsiColor bg) =>
        ToAnsiEscapeSequence(new ConsoleFormat(Foreground: fg, Background: bg));

    public static string ToAnsiEscapeSequence(ConsoleFormat formatting) =>
       Escape
        + "["
        + string.Join(
            separator: ";",
            values:
            (
                formatting.Inverted ?
                new[]
                {
                        ResetForegroundColor,
                        ResetBackgroundColor,
                        Reverse
                } :
                new[]
                {
                        formatting.Foreground?.Code?? ResetForegroundColor,
                        formatting.Background?.Code ?? ResetBackgroundColor,
                        formatting.Bold ? Bold : null,
                        formatting.Underline ? Underline : null,
                }
            ).Where(format => format is not null)
          )
        + "m";

    public readonly struct PredefinedAnsiEscapeCode
    {
        public readonly string Foreground;
        public readonly string Background;

        public PredefinedAnsiEscapeCode(AnsiColor.PredefinedAnsiColor color)
        {
            Foreground = ToAnsiEscapeSequence(new ConsoleFormat(Foreground: color.Foreground));
            Background = ToAnsiEscapeSequence(new ConsoleFormat(Background: color.Background));
        }
    }
}
