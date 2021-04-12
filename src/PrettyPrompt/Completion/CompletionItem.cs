using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrettyPrompt.Completion
{

    public delegate Task<IReadOnlyList<CompletionItem>> CompletionHandlerAsync(string text, int caret);

    public class CompletionItem
    {
        public int StartIndex { get; init; }
        public string ReplacementText { get; init; }
        public Lazy<Task<string>> ExtendedDescription { get; init; }
    }
}
