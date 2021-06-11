#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Highlighting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrettyPrompt.Tests
{
    public class SyntaxHighlighterTestData
    {
        private readonly IReadOnlyDictionary<string, AnsiColor> highlights;

        public SyntaxHighlighterTestData(IReadOnlyDictionary<string, AnsiColor> colors = null)
        {
            this.highlights = colors ?? new Dictionary<string, AnsiColor>()
            {
                { "red", AnsiColor.BrightRed },
                { "green", AnsiColor.BrightGreen },
                { "blue", AnsiColor.BrightBlue },
            };
        }

        public Task<IReadOnlyCollection<FormatSpan>> HighlightHandlerAsync(string text)
        {
            var spans = new List<FormatSpan>();

            for (int i = 0; i < text.Length; i++)
            {
                foreach (var term in highlights)
                {
                    if (text.Length >= i + term.Key.Length && text.Substring(i, term.Key.Length).ToLower() == term.Key)
                    {
                        spans.Add(new FormatSpan(i, term.Key.Length, new ConsoleFormat(Foreground: term.Value)));
                        i += term.Key.Length;
                        break;
                    }
                }
            }
            return Task.FromResult<IReadOnlyCollection<FormatSpan>>(spans);
        }
    }
}
