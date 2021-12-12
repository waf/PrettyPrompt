﻿#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;

namespace PrettyPrompt.Highlighting;

/// <summary>
/// ANSI color definitions for the terminal.
/// Each color has a different code depending on if it's applied as a foreground or background color.
/// </summary>
/// <remarks>https://en.wikipedia.org/wiki/ANSI_escape_code#Colors</remarks>
public sealed class AnsiColor : IEquatable<AnsiColor>
{
    private readonly string friendlyName;
    public string Foreground { get; }
    public string Background { get; }

    private AnsiColor(string foreground, string background, string friendlyName)
    {
        this.Foreground = foreground;
        this.Background = background;
        this.friendlyName = friendlyName;
    }

    public static readonly AnsiColor Black = new("30", "40", nameof(Black));
    public static readonly AnsiColor Red = new("31", "41", nameof(Red));
    public static readonly AnsiColor Green = new("32", "42", nameof(Green));
    public static readonly AnsiColor Yellow = new("33", "43", nameof(Yellow));
    public static readonly AnsiColor Blue = new("34", "44", nameof(Blue));
    public static readonly AnsiColor Magenta = new("35", "45", nameof(Magenta));
    public static readonly AnsiColor Cyan = new("36", "46", nameof(Cyan));
    public static readonly AnsiColor White = new("37", "47", nameof(White));
    public static readonly AnsiColor BrightBlack = new("90", "100", nameof(BrightBlack));
    public static readonly AnsiColor BrightRed = new("91", "101", nameof(BrightRed));
    public static readonly AnsiColor BrightGreen = new("92", "102", nameof(BrightGreen));
    public static readonly AnsiColor BrightYellow = new("93", "103", nameof(BrightYellow));
    public static readonly AnsiColor BrightBlue = new("94", "104", nameof(BrightBlue));
    public static readonly AnsiColor BrightMagenta = new("95", "105", nameof(BrightMagenta));
    public static readonly AnsiColor BrightCyan = new("96", "106", nameof(BrightCyan));
    public static readonly AnsiColor BrightWhite = new("97", "107", nameof(BrightWhite));

    public static AnsiColor ForegroundRgb(byte r, byte g, byte b) => new($"38;2;{r};{g};{b}", null, "RGB foreground");
    public static AnsiColor BackgroundRgb(byte r, byte g, byte b) => new(null, $"48;2;{r};{g};{b}", "RGB background");
    public static AnsiColor Rgb(
        byte foregroundR, byte foregroundG, byte foregroundB,
        byte backgroundR, byte backgroundG, byte backgroundB)
        => new($"38;2;{foregroundR};{foregroundG};{foregroundB}", $"48;2;{backgroundR};{backgroundG};{backgroundB}", "RGB");

    public override bool Equals(object obj)
    {
        return Equals(obj as AnsiColor);
    }

    public bool Equals(AnsiColor other)
    {
        return other != null &&
               Foreground == other.Foreground &&
               Background == other.Background;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Foreground, Background);
    }

    public static bool operator ==(AnsiColor left, AnsiColor right)
    {
        return EqualityComparer<AnsiColor>.Default.Equals(left, right);
    }

    public static bool operator !=(AnsiColor left, AnsiColor right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return friendlyName;
    }
}
