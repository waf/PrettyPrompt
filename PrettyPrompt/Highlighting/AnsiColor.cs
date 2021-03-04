namespace PrettyPrompt.Highlighting
{
    /// <summary>
    /// ANSI color definitions for the terminal.
    /// Each color has a different code depending on if it's applied as a foreground or background color.
    /// </summary>
    /// <remarks>https://en.wikipedia.org/wiki/ANSI_escape_code#Colors</remarks>
    public class AnsiColor
    {
        public string Foreground { get; }
        public string Background { get; }

        private AnsiColor(string foreground, string background)
        {
            this.Foreground = foreground;
            this.Background = background;
        }

        public static readonly AnsiColor Black = new AnsiColor("30", "40");
        public static readonly AnsiColor Red = new AnsiColor("31", "41");
        public static readonly AnsiColor Green = new AnsiColor("32", "42");
        public static readonly AnsiColor Yellow = new AnsiColor("33", "43");
        public static readonly AnsiColor Blue = new AnsiColor("34", "44");
        public static readonly AnsiColor Magenta = new AnsiColor("35", "45");
        public static readonly AnsiColor Cyan = new AnsiColor("36", "46");
        public static readonly AnsiColor White = new AnsiColor("37", "47");
        public static readonly AnsiColor BrightBlack = new AnsiColor("90", "100");
        public static readonly AnsiColor BrightRed = new AnsiColor("91", "101");
        public static readonly AnsiColor BrightGreen = new AnsiColor("92", "102");
        public static readonly AnsiColor BrightYellow = new AnsiColor("93", "103");
        public static readonly AnsiColor BrightBlue = new AnsiColor("94", "104");
        public static readonly AnsiColor BrightMagenta = new AnsiColor("95", "105");
        public static readonly AnsiColor BrightCyan = new AnsiColor("96", "106");
        public static readonly AnsiColor BrightWhite = new AnsiColor("97", "107");
        public static AnsiColor RGB(byte r, byte g, byte b) => new AnsiColor($"38;2;{r};{g};{b}", $"48;2;{r};{g};{b}");
    }
}
