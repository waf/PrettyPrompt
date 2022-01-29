#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrettyPrompt.Highlighting;

class SyntaxHighlighter
{
    private readonly IPromptCallbacks promptCallbacks;
    private readonly bool hasUserOptedOutFromColor;

    // quick and dirty caching, mainly to handle cases where the user enters control
    // characters (e.g. arrow keys, intellisense) that don't actually change the highlighted input
    private string previousInput;
    private IReadOnlyCollection<FormatSpan> previousOutput;

    public SyntaxHighlighter(IPromptCallbacks promptCallbacks, bool hasUserOptedOutFromColor)
    {
        this.promptCallbacks = promptCallbacks;
        this.hasUserOptedOutFromColor = hasUserOptedOutFromColor;
        this.previousInput = string.Empty;
        this.previousOutput = Array.Empty<FormatSpan>();
    }

    public async Task<IReadOnlyCollection<FormatSpan>> HighlightAsync(string input)
    {
        if (hasUserOptedOutFromColor) return Array.Empty<FormatSpan>();

        if (input.Equals(previousInput))
        {
            return previousOutput;
        }

        var highlights = await promptCallbacks.HighlightCallbackAsync(input).ConfigureAwait(false);
        previousInput = input;
        previousOutput = highlights;
        return highlights;
    }
}