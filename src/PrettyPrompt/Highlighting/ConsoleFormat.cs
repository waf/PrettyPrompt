#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

namespace PrettyPrompt.Highlighting
{
    public sealed record FormatSpan(
        int Start,
        int Length,
        ConsoleFormat Formatting
    );

    public sealed record ConsoleFormat(
        AnsiColor Foreground = null,
        AnsiColor Background = null,
        bool Bold = false,
        bool Underline = false
    );
}
