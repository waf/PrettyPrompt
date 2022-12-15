#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

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
    public KeyBindings KeyBindings { get; }

    /// <summary>
    /// Formatted prompt string to draw (e.g. "> ")
    /// </summary>
    public FormattedString Prompt { get; set; }

    private readonly ConsoleFormat completionBoxBorderFormat;
    public ref readonly ConsoleFormat CompletionBoxBorderFormat => ref completionBoxBorderFormat;

    public AnsiColor? CompletionItemDescriptionPaneBackground { get; }
    public FormattedString SelectedCompletionItemMarker { get; }
    public string UnselectedCompletionItemMarker { get; }
    public AnsiColor? SelectedCompletionItemBackground { get; }
    public AnsiColor? SelectedTextBackground { get; }

    /// <summary>
    /// How few completion items we are willing to render. If we do not have a space for rendering of
    /// completion list with <see cref="MinCompletionItemsCount"/>
    /// </summary>
    public int MinCompletionItemsCount { get; }

    public int MaxCompletionItemsCount { get; }

    /// <summary>
    /// Determines maximum verical space allocated under current input line for completion pane.
    /// </summary>
    public double ProportionOfWindowHeightForCompletionPane { get; }

    public int TabSize { get; }

    public PromptConfiguration(
        KeyBindings? keyBindings = null,
        FormattedString? prompt = null,
        ConsoleFormat? completionBoxBorderFormat = null,
        AnsiColor? completionItemDescriptionPaneBackground = null,
        FormattedString? selectedCompletionItemMarkSymbol = null,
        AnsiColor? selectedCompletionItemBackground = null,
        AnsiColor? selectedTextBackground = null,
        int minCompletionItemsCount = 1,
        int maxCompletionItemsCount = 9, //9 is VS default
        double proportionOfWindowHeightForCompletionPane = 0.9,
        int tabSize = 4)
    {
        if (minCompletionItemsCount < 1) throw new ArgumentException("must be >=1", nameof(minCompletionItemsCount));
        if (maxCompletionItemsCount < minCompletionItemsCount) throw new ArgumentException("must be >=minCompletionItemsCount", nameof(maxCompletionItemsCount));
        if (proportionOfWindowHeightForCompletionPane is <= 0 or >= 1) throw new ArgumentException("must be >0 and <1", nameof(proportionOfWindowHeightForCompletionPane));

        KeyBindings = keyBindings ?? new KeyBindings();
        Prompt = prompt ?? "> ";

        this.completionBoxBorderFormat = GetFormat(completionBoxBorderFormat ?? new ConsoleFormat(Foreground: AnsiColor.Blue));
        CompletionItemDescriptionPaneBackground = GetColor(completionItemDescriptionPaneBackground);

        SelectedCompletionItemMarker = selectedCompletionItemMarkSymbol ?? new FormattedString(">", new FormatSpan(0, 1, AnsiColor.Cyan));
        UnselectedCompletionItemMarker = new string(' ', SelectedCompletionItemMarker.Length);
        SelectedCompletionItemBackground = GetColor(selectedCompletionItemBackground);
        SelectedTextBackground = GetColor(selectedTextBackground);

        ConsoleFormat GetFormat(ConsoleFormat format) => HasUserOptedOutFromColor ? ConsoleFormat.None : format;
        AnsiColor? GetColor(AnsiColor? color) => HasUserOptedOutFromColor ? null : color;

        MinCompletionItemsCount = minCompletionItemsCount;
        MaxCompletionItemsCount = maxCompletionItemsCount;
        ProportionOfWindowHeightForCompletionPane = proportionOfWindowHeightForCompletionPane;

        TabSize = tabSize;
    }
}