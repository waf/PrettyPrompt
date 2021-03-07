using PrettyPrompt.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PrettyPrompt
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Welcome! Try typing some fruit names.");
            Console.WriteLine();

            var prompt = new Prompt(completionHandler: FindCompletions, highlightHandler: Highlight);
            while (true)
            {
                var response = await prompt.ReadLine("> ");
                if(response.Success)
                {
                    if (response.Text == "exit") break;

                    Console.WriteLine("You wrote " + response.Text);
                }
            }
        }

        // demo data
        private static Dictionary<string, AnsiColor> Fruits = new()
        {
            { "apple", AnsiColor.BrightRed },
            { "apricot", AnsiColor.Yellow },
            { "avocado", AnsiColor.Green },
            { "banana", AnsiColor.BrightYellow },
            { "cantaloupe", AnsiColor.Green },
            { "grapefruit", AnsiColor.RGB(224, 112, 124) },
            { "grape", AnsiColor.Blue },
            { "mango", AnsiColor.Yellow },
            { "melon", AnsiColor.Green },
            { "orange", AnsiColor.RGB(255, 165, 0) },
            { "pear", AnsiColor.Green },
            { "peach", AnsiColor.RGB(255, 229, 180) },
            { "pineapple", AnsiColor.BrightYellow },
            { "strawberry", AnsiColor.BrightRed },
        };

        private static Task<IReadOnlyList<Completion>> FindCompletions(string typedInput, int caret)
        {
            var textUntilCaret = typedInput.Substring(0, caret);
            var previousWordStart = textUntilCaret.LastIndexOfAny(new[] { ' ', '\n', '.', '(', ')' });
            var typedWord = previousWordStart == -1
                ? textUntilCaret.ToLower()
                : textUntilCaret.Substring(previousWordStart + 1).ToLower();
            return Task.FromResult<IReadOnlyList<Completion>>(
                Fruits.Keys
                .Where(fruit => fruit.StartsWith(typedWord))
                .Select(fruit => new Completion
                {
                    StartIndex = previousWordStart + 1,
                    ReplacementText = fruit
                })
                .ToArray()
            );
        }

        private static Task<IReadOnlyCollection<FormatSpan>> Highlight(string text)
        {
            var spans = new List<FormatSpan>();

            for (int i = 0; i < text.Length; i++)
            {
                foreach (var fruit in Fruits)
                {
                    if(text.Length >= i + fruit.Key.Length && text.Substring(i, fruit.Key.Length).ToLower() == fruit.Key)
                    {
                        spans.Add(new FormatSpan(i, fruit.Key.Length, new ConsoleFormat(foreground: fruit.Value)));
                        i += fruit.Key.Length;
                        break;
                    }
                }
            }
            return Task.FromResult<IReadOnlyCollection<FormatSpan>>(spans);
        }
    }
}
