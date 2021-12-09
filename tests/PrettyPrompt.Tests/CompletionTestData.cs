#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Completion;
using PrettyPrompt.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PrettyPrompt.Tests;

public class CompletionTestData
{
    private readonly IReadOnlyCollection<string> completions;

    public CompletionTestData(IReadOnlyCollection<string> completions = null)
    {
        this.completions = completions ?? new[] { "Aardvark", "Albatross", "Alligator", "Alpaca", "Ant", "Anteater", "Baboon", "Cat", "Dog", "Elephant", "Fox", "Zebra" };
    }

    public Task<IReadOnlyList<CompletionItem>> CompletionHandlerAsync(string text, int caret)
    {
        var textUntilCaret = text.Substring(0, caret);
        var previousWordStart = textUntilCaret.LastIndexOfAny(new[] { ' ', '\n', '.', '(', ')' });
        var typedWord = previousWordStart == -1
            ? textUntilCaret
            : textUntilCaret.Substring(previousWordStart + 1);
        return Task.FromResult<IReadOnlyList<CompletionItem>>(
            completions
                .Where(c => c.StartsWith(typedWord, StringComparison.CurrentCultureIgnoreCase))
                .Select((c, i) => new CompletionItem
                {
                    StartIndex = previousWordStart + 1,
                    ReplacementText = c,
                    DisplayText = i % 2 == 0 ? c : null, // display text is optional, ReplacementText should be used when this is null.
                        ExtendedDescription = new Lazy<Task<FormattedString>>(() => Task.FromResult<FormattedString>("a vivid description of " + c))
                })
                .ToArray()
        );
    }
}
