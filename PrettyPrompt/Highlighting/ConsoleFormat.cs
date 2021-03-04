namespace PrettyPrompt.Highlighting
{
    public class ConsoleFormat
    {
        public ConsoleFormat(AnsiColor foreground = null, AnsiColor background = null, bool bold = false, bool underline = false)
        {
            Foreground = foreground;
            Background = background;
            Bold = bold;
            Underline = underline;
        }

        public AnsiColor Foreground { get; }
        public AnsiColor Background { get; }
        public bool Bold { get; }
        public bool Underline { get; }
    }
}
