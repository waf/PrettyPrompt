#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PrettyPrompt.Highlighting
{
    class SyntaxHighlighter
    {
        private readonly HighlightCallbackAsync highlightCallbackAsync;

        // quick and dirty caching, mainly to handle cases where the user enters control
        // characters (e.g. arrow keys, intellisense) that don't actually change the highlighted input
        private string previousInput;
        private IReadOnlyCollection<FormatSpan> previousOutput;

        public SyntaxHighlighter(HighlightCallbackAsync highlightCallbackAsync)
        {
            this.highlightCallbackAsync = highlightCallbackAsync;
            this.previousInput = string.Empty;
            this.previousOutput = Array.Empty<FormatSpan>();
        }

        public async Task<IReadOnlyCollection<FormatSpan>> HighlightAsync(StringBuilder input)
        {
            string thisInput = input.ToString();

            if (thisInput.Equals(previousInput))
            {
                return previousOutput;
            }

            var highlights = await highlightCallbackAsync.Invoke(thisInput).ConfigureAwait(false);
            previousInput = thisInput;
            previousOutput = highlights;
            return highlights;
        }
    }
}
