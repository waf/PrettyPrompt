#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Text;
using PrettyPrompt.Documents;

namespace PrettyPrompt.Highlighting;

public readonly struct FormattedStringBuilder
{
    private readonly StringBuilder stringBuilder = new();
    private readonly List<FormatSpan> formatSpans = new();

    public FormattedStringBuilder() { } //this line is needed in GitHub CI build, but not needed in VS 17.0.4

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

    public FormattedStringBuilder Append(string text, ConsoleFormat format)
        => Append(text, new FormatSpan(new TextSpan(0, text.Length), format));

    public void Clear()
    {
        stringBuilder.Clear();
        formatSpans.Clear();
    }
    
    public FormattedString ToFormattedString()
        => new(stringBuilder.ToString(), formatSpans);

    public override string ToString()
        => stringBuilder.ToString();
}