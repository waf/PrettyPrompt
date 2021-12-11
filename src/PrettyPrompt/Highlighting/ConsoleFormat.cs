#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

namespace PrettyPrompt.Highlighting;

public readonly record struct ConsoleFormat(
    AnsiColor Foreground = null,
    AnsiColor Background = null,
    bool Bold = false,
    bool Underline = false,
    bool Inverted = false)
{
    public static ConsoleFormat None => default;
}