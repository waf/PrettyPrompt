#nullable enable
using System;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt;

public class PromptConfiguration
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

    public ConsoleFormat CompletionBoxBorderFormat { get; }
    public AnsiColor? CompletionItemDocumentationPaneBackground { get; }

    public FormattedString SelectedCompletionItemMarker { get; }
    public string UnselectedCompletionItemMarker { get; }
    public AnsiColor? SelectedCompletionItemBackground { get; }

    /// <summary>
    /// How few completion items we are willing to render.
    /// </summary>
    public int MinCompletionItemsCount { get; }

    public int MaxCompletionItemsCount { get; }

    /// <summary>
    /// Determines maximum verical space allocated under current input line for completion pane.
    /// </summary>
    public double ProportionOfWindowHeightForCompletionPane { get; }

    public PromptConfiguration(
        string prompt = "> ",
        ConsoleFormat? completionBoxBorderFormat = null,
        AnsiColor? completionItemDocumentationPaneBackground = null,
        FormattedString? selectedCompletionItemMarkSymbol = null,
        AnsiColor? selectedCompletionItemBackground = null,
        int minCompletionItemsCount = 1,
        int maxCompletionItemsCount = 9, //9 is VS default
        double proportionOfWindowHeightForCompletionPane = 0.9)
    {
        if (minCompletionItemsCount < 1) throw new ArgumentException("must be >=1", nameof(minCompletionItemsCount));
        if (maxCompletionItemsCount < minCompletionItemsCount) throw new ArgumentException("must be >=minCompletionItemsCount", nameof(maxCompletionItemsCount));
        if (proportionOfWindowHeightForCompletionPane is <= 0 or >= 1) throw new ArgumentException("must be >0 and <1", nameof(proportionOfWindowHeightForCompletionPane));

        Prompt = prompt;

        CompletionBoxBorderFormat = GetFormat(completionBoxBorderFormat ?? new ConsoleFormat(Foreground: AnsiColor.Blue));
        CompletionItemDocumentationPaneBackground = GetColor(completionItemDocumentationPaneBackground ?? AnsiColor.RGB(30, 30, 30));

        SelectedCompletionItemMarker = selectedCompletionItemMarkSymbol ?? new FormattedString(">", new FormatSpan(0, 1, new ConsoleFormat(Foreground: AnsiColor.Cyan)));
        UnselectedCompletionItemMarker = new string(' ', SelectedCompletionItemMarker.Length);
        SelectedCompletionItemBackground = GetColor(selectedCompletionItemBackground ?? AnsiColor.RGB(30, 30, 30));

        ConsoleFormat GetFormat(ConsoleFormat format) => HasUserOptedOutFromColor ? ConsoleFormat.None : format;
        AnsiColor? GetColor(AnsiColor color) => HasUserOptedOutFromColor ? null : color;

        MinCompletionItemsCount = minCompletionItemsCount;
        MaxCompletionItemsCount = maxCompletionItemsCount;
        ProportionOfWindowHeightForCompletionPane = proportionOfWindowHeightForCompletionPane;
    }
}