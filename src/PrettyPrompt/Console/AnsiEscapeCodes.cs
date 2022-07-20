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
    private const char ResetChar = '0';
    private const char Bold = '1';
    private const char Underline = '4';
    private const string Reverse = "7";
    public const string ClearLine = $"{Escape}[0K";
    public const string ClearToEndOfScreen = $"{Escape}[0J";
    public const string ClearEntireScreen = $"{Escape}[2J";
    public static readonly string Reset = $"{Escape}[{ResetChar}m";

    /// <summary>
    /// Index starts at 1!
    /// </summary>
    public static string GetMoveCursorToColumn(int index) => $"{Escape}[{index}G";

    public static string GetMoveCursorUp(int count) => count == 0 ? "" : $"{Escape}[{count}A";
    public static string GetMoveCursorDown(int count) => count == 0 ? "" : $"{Escape}[{count}B";
    public static string GetMoveCursorRight(int count) => count == 0 ? "" : $"{Escape}[{count}C";
    public static string GetMoveCursorLeft(int count) => count == 0 ? "" : $"{Escape}[{count}D";

    public static void AppendMoveCursorToColumn(StringBuilder sb, int index) => MoveCursor(sb, index, 'G');
    public static void AppendMoveCursorUp(StringBuilder sb, int count) => MoveCursor(sb, count, 'A');
    public static void AppendMoveCursorDown(StringBuilder sb, int count) => MoveCursor(sb, count, 'B');
    public static void AppendMoveCursorRight(StringBuilder sb, int count) => MoveCursor(sb, count, 'C');
    public static void AppendMoveCursorLeft(StringBuilder sb, int count) => MoveCursor(sb, count, 'D');
    private static void MoveCursor(StringBuilder sb, int count, char direction)
    {
        if (count > 0)
        {
            sb.Append(EscapeChar);
            sb.Append('[');
            sb.Append(count);
            sb.Append(direction);
        }
    }

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
            stringBuilder.Append(ResetChar);

            if (formatting.ForegroundCode != null)
            {
                stringBuilder.Append(';');
                stringBuilder.Append(formatting.ForegroundCode);
            }

            if (formatting.BackgroundCode != null)
            {
                stringBuilder.Append(';');
                stringBuilder.Append(formatting.BackgroundCode);
            }

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