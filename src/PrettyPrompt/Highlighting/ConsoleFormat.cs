#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

namespace PrettyPrompt.Highlighting;

public readonly record struct ConsoleFormat(
    AnsiColor? Foreground = null,
    AnsiColor? Background = null,
    bool Bold = false,
    bool Underline = false,
    bool Inverted = false)
{
    public static ConsoleFormat None => default;

    public string? ForegroundCode => Foreground?.GetCode(AnsiColor.Type.Foreground);
    public string? BackgroundCode => Background?.GetCode(AnsiColor.Type.Background);

    public readonly bool Equals(in ConsoleFormat other)
    {
        //this is hot from IncrementalRendering.CalculateDiff, so we want to use custom Equals where 'other' is by-ref
        return
            Foreground == other.Foreground &&
            Background == other.Background &&
            Bold == other.Bold &&
            Underline == other.Underline &&
            Inverted == other.Inverted;
    }

    public bool IsDefault =>
        !Foreground.HasValue &&
        !Background.HasValue &&
        !Bold &&
        !Underline &&
        !Inverted;
}