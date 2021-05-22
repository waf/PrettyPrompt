namespace PrettyPrompt.Highlighting
{
    public sealed record FormatSpan(
        int Start,
        int Length,
        ConsoleFormat Formatting
    );

    public sealed record ConsoleFormat(
        AnsiColor Foreground = null,
        AnsiColor Background = null,
        bool Bold = false,
        bool Underline = false
    );
}
