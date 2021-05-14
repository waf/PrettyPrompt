using PrettyPrompt.Completion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PrettyPrompt.Tests
{
    public static class CompletionTestData
    {
        private static readonly IReadOnlyCollection<string> completions = new[]
        {
            "Aardvark", "Albatross", "Alligator", "Alpaca", "Ant", "Anteater", "Zebra"
        };

        public static Task<IReadOnlyList<CompletionItem>> CompletionHandlerAsync(string text, int caret)
        {
            var textUntilCaret = text.Substring(0, caret);
            var previousWordStart = textUntilCaret.LastIndexOfAny(new[] { ' ', '\n', '.', '(', ')' });
            var typedWord = previousWordStart == -1
                ? textUntilCaret
                : textUntilCaret.Substring(previousWordStart + 1);
            return Task.FromResult<IReadOnlyList<CompletionItem>>(
                completions
                    .Where(c => c.StartsWith(typedWord, StringComparison.CurrentCultureIgnoreCase))
                    .Select(c => new CompletionItem
                    {
                        StartIndex = previousWordStart + 1,
                        ReplacementText = c,
                        DisplayText = c,
                        ExtendedDescription = new Lazy<Task<string>>(() => Task.FromResult("a vivid description of " + c))
                    })
                    .ToArray()
            );
        }
    }
}
