#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Text;

namespace PrettyPrompt.Highlighting;

public readonly struct FormattedStringBuilder
{
    private readonly StringBuilder stringBuilder = new();
    private readonly List<FormatSpan> formatSpans = new();

    public int Length => stringBuilder.Length;

    public FormattedStringBuilder Append(FormattedString text)
    {
        foreach (var span in text.FormatSpans)
        {
            formatSpans.Add(span.Offset(stringBuilder.Length));
        }
        stringBuilder.Append(text.Text);
        return this;
    }

    public FormattedStringBuilder Append(string text)
    {
        stringBuilder.Append(text);
        return this;
    }

    public FormattedStringBuilder Append(string text, params FormatSpan[] formatSpans)
        => Append(new FormattedString(text, formatSpans));

    public void Clear()
    {
        stringBuilder.Clear();
        formatSpans.Clear();
    }

    public FormattedString ToFormattedString()
        => new(stringBuilder.ToString(), formatSpans);
}