using PrettyPrompt.Highlighting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrettyPrompt.Tests
{
    public static class SyntaxHighlighterTestData
    {
        private static readonly IReadOnlyDictionary<string, AnsiColor> highlights = new Dictionary<string, AnsiColor>()
        {
            { "red", AnsiColor.BrightRed },
            { "green", AnsiColor.BrightGreen },
            { "blue", AnsiColor.BrightBlue },
        };

        public static Task<IReadOnlyCollection<FormatSpan>> HighlightHandlerAsync(string text)
        {
            var spans = new List<FormatSpan>();

            for (int i = 0; i < text.Length; i++)
            {
                foreach (var term in highlights)
                {
                    if(text.Length >= i + term.Key.Length && text.Substring(i, term.Key.Length).ToLower() == term.Key)
                    {
                        spans.Add(new FormatSpan(i, term.Key.Length, new ConsoleFormat(foreground: term.Value)));
                        i += term.Key.Length;
                        break;
                    }
                }
            }
            return Task.FromResult<IReadOnlyCollection<FormatSpan>>(spans);
        }
    }
}
