using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt;

internal static class Program
{
    private static async Task Main(string[] _)
    {
        Console.WriteLine("Welcome! Try typing some fruit names.");
        Console.WriteLine();

        var prompt = new Prompt(
            persistentHistoryFilepath: "./history-file",
            callbacks: new FruitPromptCallbacks(),
            configuration: new PromptConfiguration(
                prompt: new FormattedString(">>> ", new FormatSpan(0, 1, AnsiColor.Red), new FormatSpan(1, 1, AnsiColor.Yellow), new FormatSpan(2, 1, AnsiColor.Green)),
                completionItemDescriptionPaneBackground: AnsiColor.Rgb(30, 30, 30),
                selectedCompletionItemBackground: AnsiColor.Rgb(30, 30, 30)));
        while (true)
        {
            var response = await prompt.ReadLineAsync().ConfigureAwait(false);
            if (response.IsSuccess)
            {
                if (response.Text == "exit") break;
                // optionally, use response.CancellationToken so the user can
                // cancel long-running processing of their response via ctrl-c
                Console.WriteLine("You wrote " + (response.SubmitKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) ? response.Text.ToUpper() : response.Text));
            }
        }
    }

    // demo data
    private static readonly (string Name, string Description, AnsiColor Highlight)[] Fruits = new[]
    {
        ("apple", "the round fruit of a tree of the rose family, which typically has thin red or green skin and crisp flesh. Many varieties have been developed as dessert or cooking fruit or for making cider.", AnsiColor.BrightRed ),
        ("apricot", "a juicy, soft fruit, resembling a small peach, of an orange-yellow color.", AnsiColor.Yellow ),
        ("avocado", "a pear-shaped fruit with a rough leathery skin, smooth oily edible flesh, and a large stone.", AnsiColor.Green ),
        ("banana", "a long curved fruit which grows in clusters and has soft pulpy flesh and yellow skin when ripe.", AnsiColor.BrightYellow ),
        ("cantaloupe", "a small round melon of a variety with orange flesh and ribbed skin.", AnsiColor.Green ),
        ("grapefruit", "a large round yellow citrus fruit with an acid juicy pulp.", AnsiColor.Rgb(224, 112, 124) ),
        ("grape", "a berry, typically green (classified as white), purple, red, or black, growing in clusters on a grapevine, eaten as fruit, and used in making wine.", AnsiColor.Blue ),
        ("mango", "a fleshy, oval, yellowish-red tropical fruit that is eaten ripe or used green for pickles or chutneys.", AnsiColor.Yellow ),
        ("melon", "the large round fruit of a plant of the gourd family, with sweet pulpy flesh and many seeds.", AnsiColor.Green ),
        ("orange", "a round juicy citrus fruit with a tough bright reddish-yellow rind.", AnsiColor.Rgb(255, 165, 0) ),
        ("pear", "a yellowish- or brownish-green edible fruit that is typically narrow at the stalk and wider toward the base, with sweet, slightly gritty flesh.", AnsiColor.Green ),
        ("peach", "a round stone fruit with juicy yellow flesh and downy pinkish-yellow skin.", AnsiColor.Rgb(255, 229, 180) ),
        ("pineapple", "a large juicy tropical fruit consisting of aromatic edible yellow flesh surrounded by a tough segmented skin and topped with a tuft of stiff leaves.", AnsiColor.BrightYellow ),
        ("strawberry", "a sweet soft red fruit with a seed-studded surface.", AnsiColor.BrightRed ),
    };

    private static readonly (string Name, AnsiColor Color)[] ColorsToHighlight = new[]
    {
        ("red", AnsiColor.Red),
        ("green", AnsiColor.Green),
        ("yellow", AnsiColor.Yellow),
        ("blue", AnsiColor.Blue),
        ("purple", AnsiColor.Rgb(72, 0, 255)),
        ("orange", AnsiColor.Rgb(255, 165, 0)),
    };

    private static FormattedString GetFormattedString(string text)
        => new(text, EnumerateFormatSpans(text, ColorsToHighlight));

    private static IEnumerable<FormatSpan> EnumerateFormatSpans(string text, IEnumerable<(string TextToFormat, AnsiColor Color)> formattingInfo)
    {
        foreach (var (textToFormat, color) in formattingInfo)
        {
            int startIndex;
            int offset = 0;
            while ((startIndex = text.AsSpan(offset).IndexOf(textToFormat)) != -1)
            {
                yield return new FormatSpan(offset + startIndex, textToFormat.Length, color);
                offset += startIndex + textToFormat.Length;
            }
        }
    }

    private static Task<KeyPressCallbackResult?> ShowFruitDocumentation(string text, int caret, CancellationToken cancellationToken)
    {
        string wordUnderCursor = GetWordAtCaret(text, caret).ToLower();

        if (Fruits.Any(f => f.Name.ToLower() == wordUnderCursor))
        {
            // wikipedia is the definitive fruit documentation.
            LaunchBrowser("https://en.wikipedia.org/wiki/" + Uri.EscapeUriString(wordUnderCursor));
        }

        // since we return a null KeyPressCallbackResult here, the user will remain on the current prompt
        // and will still be able to edit the input.
        // if we were to return a non-null result, this result will be returned from ReadLineAsync(). This
        // is useful if we want our custom keypress to submit the prompt and control the output manually.
        return Task.FromResult<KeyPressCallbackResult?>(null);

        // local functions
        static string GetWordAtCaret(string text, int caret)
        {
            var words = text.Split(new[] { ' ', '\n' });
            string wordAtCaret = string.Empty;
            int currentIndex = 0;
            foreach (var word in words)
            {
                if (currentIndex < caret && caret < currentIndex + word.Length)
                {
                    wordAtCaret = word;
                    break;
                }
                currentIndex += word.Length + 1; // +1 due to word separator
            }

            return wordAtCaret;
        }

        static void LaunchBrowser(string url)
        {
            var browser =
                OperatingSystem.IsWindows() ? new ProcessStartInfo("explorer", $"{url}") : // using cmd will cancel TreatControlCAsInput. We don't want that.
                OperatingSystem.IsMacOS() ? new ProcessStartInfo("open", url) :
                new ProcessStartInfo("xdg-open", url); //linux, unix-like

            Process.Start(browser)?.WaitForExit();
        }
    }

    private class FruitPromptCallbacks : PromptCallbacks
    {
        protected override IEnumerable<(KeyPressPattern Pattern, KeyPressCallbackAsync Callback)> GetKeyPressCallbacks()
        {
            // registers functions to be called when the user presses a key. The text
            // currently typed into the prompt, along with the caret position within
            // that text are provided as callback parameters.
            yield return (new(ConsoleModifiers.Control, ConsoleKey.F1), ShowFruitDocumentation);
        }

        protected override Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
        {
            // demo completion algorithm callback
            // populate completions and documentation for autocompletion window
            var typedWord = text.AsSpan(spanToBeReplaced.Start, spanToBeReplaced.Length).ToString();
            return Task.FromResult<IReadOnlyList<CompletionItem>>(
                Fruits
                .Select(fruit =>
                {
                    var displayText = new FormattedString(fruit.Name, new FormatSpan(0, fruit.Name.Length, fruit.Highlight));
                    var description = GetFormattedString(fruit.Description);
                    return new CompletionItem(
                        replacementText: fruit.Name,
                        displayText: displayText,
                        getExtendedDescription: _ => Task.FromResult(description)
                    );
                })
                .ToArray()
            );
        }

        protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
        {
            // demo syntax highlighting callback
            IReadOnlyCollection<FormatSpan> spans = EnumerateFormatSpans(text, Fruits.Select(f => (f.Name, f.Highlight))).ToList();
            return Task.FromResult(spans);
        }
    }
}