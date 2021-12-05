#nullable enable
using System;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt
{
    public class PromptTheme
    {
        /// <summary>
        /// <code>true</code> if the user opted out of color, via an environment variable as specified by https://no-color.org/.
        /// PrettyPrompt will automatically disable colors in this case. You can read this property to control other colors in
        /// your application.
        /// </summary>
        public static bool HasUserOptedOutFromColor { get; } = Environment.GetEnvironmentVariable("NO_COLOR") is not null;

        /// <summary>
        /// The prompt string to draw (e.g. "> ")
        /// </summary>
        public string Prompt { get; }

        public ConsoleFormat CompletionBorder { get; }
        public ConsoleFormat DocumentationBorder { get; }

        public FormattedString SelectedCompletionItemMarker { get; }
        public string UnselectedCompletionItemMarker { get; }
        public AnsiColor? SelectedCompletionItemBackground { get; }

        public PromptTheme(
            string prompt = "> ",
            ConsoleFormat? completionBorder = null,
            ConsoleFormat? documentationBorder = null,
            FormattedString? selectedCompletionItemMarkSymbol = null,
            AnsiColor? selectedCompletionItemBackground = null)
        {
            Prompt = prompt;

            CompletionBorder = GetFormat(completionBorder ?? new ConsoleFormat(Foreground: AnsiColor.Blue));
            DocumentationBorder = GetFormat(documentationBorder ?? new ConsoleFormat(Foreground: AnsiColor.Cyan));

            SelectedCompletionItemMarker = selectedCompletionItemMarkSymbol ?? new FormattedString(">", new FormatSpan(0, 1, new ConsoleFormat(Foreground: DocumentationBorder.Foreground)));
            UnselectedCompletionItemMarker = new string(' ', SelectedCompletionItemMarker.Length);
            SelectedCompletionItemBackground = GetColor(selectedCompletionItemBackground ?? AnsiColor.RGB(40, 30, 30));

            ConsoleFormat GetFormat(ConsoleFormat format) => HasUserOptedOutFromColor ? ConsoleFormat.None : format;
            AnsiColor? GetColor(AnsiColor color) => HasUserOptedOutFromColor ? null : color;
        }
    }
}