#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Tests;

public class CompletionTestData
{
    private readonly IReadOnlyCollection<string> completions;

    public CompletionTestData()
        : this(null)
    { }

    public CompletionTestData(params string[]? completions)
    {
        this.completions = completions ?? new[] { "Aardvark", "Albatross", "Alligator", "Alpaca", "Ant", "Anteater", "Baboon", "Cat", "Dog", "Elephant", "Fox", "Zebra" };
    }

    public Task<IReadOnlyList<CompletionItem>> CompletionHandlerAsync(string typedInput, int caret)
    {
        var nonWordChars = new[] { ' ', '\n', '.', '(', ')' };

        var wordStart = typedInput.AsSpan(0, caret).LastIndexOfAny(nonWordChars);
        wordStart = wordStart >= 0 ? wordStart + 1 : 0;

        var wordEnd = typedInput.AsSpan(caret).IndexOfAny(nonWordChars);
        wordEnd = wordEnd >= 0 ? wordEnd : typedInput.Length;

        var typedWord = typedInput.AsSpan(wordStart, wordEnd - wordStart).ToString();

        return Task.FromResult<IReadOnlyList<CompletionItem>>(
            completions
                .Where(c => c.StartsWith(typedWord, StringComparison.CurrentCultureIgnoreCase))
                .Select((c, i) => new CompletionItem(
                    replacementText: c,
                    displayText: i % 2 == 0 ? c : null, // display text is optional, ReplacementText should be used when this is null.
                    extendedDescription: new Lazy<Task<FormattedString>>(() => Task.FromResult<FormattedString>("a vivid description of " + c))
                ))
                .ToArray()
        );
    }
}