#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Text;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Consoles;
public static class AnsiEscapeCodes
{
    private const char EscapeChar = '\u001b';
    private const string Escape = "\u001b";
    private const string ResetForegroundColor = "39";
    private const string ResetBackgroundColor = "49";
    private const char Bold = '1';
    private const char Underline = '4';
    private const string Reverse = "7";
    public const string ClearLine = $"{Escape}[0K";
    public const string ClearToEndOfScreen = $"{Escape}[0J";
    public const string ClearEntireScreen = $"{Escape}[2J";

    /// <summary>
    /// Index starts at 1!
    /// </summary>
    public static string MoveCursorToColumn(int index) => $"{Escape}[{index}G";

    public static string MoveCursorUp(int count) => count == 0 ? "" : $"{Escape}[{count}A";
    public static string MoveCursorDown(int count) => count == 0 ? "" : $"{Escape}[{count}B";
    public static string MoveCursorRight(int count) => count == 0 ? "" : $"{Escape}[{count}C";
    public static string MoveCursorLeft(int count) => count == 0 ? "" : $"{Escape}[{count}D";

    public static readonly string Reset = $"{Escape}[0m";

    internal static string ToAnsiEscapeSequence(string colorCode) => $"{Escape}[{colorCode}m";

    public static string ToAnsiEscapeSequenceSlow(ConsoleFormat formatting)
    {
        var sb = new StringBuilder();
        AppendAnsiEscapeSequence(sb, formatting);
        return sb.ToString();
    }

    public static void AppendAnsiEscapeSequence(StringBuilder stringBuilder, ConsoleFormat formatting)
    {
        stringBuilder.Append(EscapeChar);
        stringBuilder.Append('[');
        if (formatting.Inverted)
        {
            const string ResetAndReverse = ResetForegroundColor + ";" + ResetBackgroundColor + ";" + Reverse;
            stringBuilder.Append(ResetAndReverse);
        }
        else
        {
            stringBuilder.Append(formatting.ForegroundCode ?? ResetForegroundColor);
            stringBuilder.Append(';');
            stringBuilder.Append(formatting.BackgroundCode ?? ResetBackgroundColor);

            if (formatting.Bold)
            {
                stringBuilder.Append(';');
                stringBuilder.Append(Bold);
            }

            if (formatting.Underline)
            {
                stringBuilder.Append(';');
                stringBuilder.Append(Underline);
            }
        }
        stringBuilder.Append('m');
    }
}