using PrettyPrompt.Highlighting;
using System.Linq;

namespace PrettyPrompt.Consoles
{
    internal static class AnsiEscapeCodes
    {
        private const char Escape = '\u001b';
        private const string ResetForegroundColor = "39";
        private const string ResetBackgroundColor = "49";
        public static readonly string ClearLine = $"{Escape}[0K";
        public static readonly string ClearToEndOfScreen = $"{Escape}[0J";
        public static readonly string ClearEntireScreen = $"{Escape}[2J";

        /// <summary>
        /// index starts at 1!
        /// </summary>
        public static string MoveCursorToColumn(int index) => $"{Escape}[{index}G";

        /// <summary>
        /// row and column are 1-indexed
        /// </summary>
        public static string MoveCursorToPosition(int row, int column) => $"{Escape}[{row};{column}H";
        public static string MoveCursorToPosition(ConsoleCoordinate coord) => MoveCursorToPosition(coord.Row, coord.Column);

        public static string MoveCursorUp(int count) => count == 0 ? "" : $"{Escape}[{count}A";
        public static string MoveCursorDown(int count) => count == 0 ? "" : $"{Escape}[{count}B";
        public static string MoveCursorRight(int count) => count == 0 ? "" : $"{Escape}[{count}C";
        public static string MoveCursorLeft(int count) => count == 0 ? "" : $"{Escape}[{count}D";

        public static string ScrollUp(int count) => count == 0 ? "" : $"{Escape}[{count}S";
        public static string ScrollDown(int count) => count == 0 ? "" : $"{Escape}[{count}T";

        public static string ForegroundColor(byte r, byte g, byte b) => ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.RGB(r, g, b)));
        public static string BackgroundColor(byte r, byte g, byte b) => ToAnsiEscapeSequence(new ConsoleFormat(background: AnsiColor.RGB(r, g, b)));

        public static readonly string Black = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.Black));
        public static readonly string Red = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.Red));
        public static readonly string Green = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.Green));
        public static readonly string Yellow = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.Yellow));
        public static readonly ConsoleFormat Blue = new ConsoleFormat(foreground: AnsiColor.Blue);
        public static readonly string Magenta = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.Magenta));
        public static readonly string Cyan = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.Cyan));
        public static readonly string White = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.White));
        public static readonly string BrightBlack = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightBlack));
        public static readonly string BrightRed = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightRed));
        public static readonly string BrightGreen = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightGreen));
        public static readonly string BrightYellow = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightYellow));
        public static readonly string BrightBlue = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightBlue));
        public static readonly string BrightMagenta = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightMagenta));
        public static readonly string BrightCyan = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightCyan));
        public static readonly string BrightWhite = ToAnsiEscapeSequence(new ConsoleFormat(foreground: AnsiColor.BrightWhite));

        public static string SetColors(AnsiColor fg, AnsiColor bg) =>
            ToAnsiEscapeSequence(new ConsoleFormat(foreground: fg, background: bg));

        public static readonly string ResetFormatting = $"{Escape}[0m";

        public static string ToAnsiEscapeSequence(ConsoleFormat formatting) =>
           Escape
            + "["
            + string.Join(
                separator: ";",
                values: new[]
                {
                    formatting.Foreground?.Foreground ?? ResetForegroundColor,
                    formatting.Background?.Background ?? ResetBackgroundColor,
                    formatting.Bold ? "1" : null,
                    formatting.Underline ? "4" : null
                }.Where(format => format is not null)
              )
            + "m";
    }
}
