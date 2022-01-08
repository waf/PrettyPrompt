#region License Header
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

    public static readonly string Reset = $"{Escape}[0m";

    internal static string ToAnsiEscapeSequence(string colorCode) => $"{Escape}[{colorCode}m";

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
                        formatting.ForegroundCode ?? ResetForegroundColor,
                        formatting.BackgroundCode ?? ResetBackgroundColor,
                        formatting.Bold ? Bold : null,
                        formatting.Underline ? Underline : null,
                }
            ).Where(format => format is not null)
          )
        + "m";
}