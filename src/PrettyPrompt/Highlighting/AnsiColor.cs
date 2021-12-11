#region License Header
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
    public string Code { get; }
    public bool IsForeground { get; }
    public bool IsBackground => !IsForeground;

    private AnsiColor(string code, bool isForeground, string friendlyName)
    {
        this.Code = code;
        this.IsForeground = isForeground;
        this.friendlyName = friendlyName;
    }

    public static readonly PredefinedAnsiColor Black = new("30", "40", nameof(Black));
    public static readonly PredefinedAnsiColor Red = new("31", "41", nameof(Red));
    public static readonly PredefinedAnsiColor Green = new("32", "42", nameof(Green));
    public static readonly PredefinedAnsiColor Yellow = new("33", "43", nameof(Yellow));
    public static readonly PredefinedAnsiColor Blue = new("34", "44", nameof(Blue));
    public static readonly PredefinedAnsiColor Magenta = new("35", "45", nameof(Magenta));
    public static readonly PredefinedAnsiColor Cyan = new("36", "46", nameof(Cyan));
    public static readonly PredefinedAnsiColor White = new("37", "47", nameof(White));
    public static readonly PredefinedAnsiColor BrightBlack = new("90", "100", nameof(BrightBlack));
    public static readonly PredefinedAnsiColor BrightRed = new("91", "101", nameof(BrightRed));
    public static readonly PredefinedAnsiColor BrightGreen = new("92", "102", nameof(BrightGreen));
    public static readonly PredefinedAnsiColor BrightYellow = new("93", "103", nameof(BrightYellow));
    public static readonly PredefinedAnsiColor BrightBlue = new("94", "104", nameof(BrightBlue));
    public static readonly PredefinedAnsiColor BrightMagenta = new("95", "105", nameof(BrightMagenta));
    public static readonly PredefinedAnsiColor BrightCyan = new("96", "106", nameof(BrightCyan));
    public static readonly PredefinedAnsiColor BrightWhite = new("97", "107", nameof(BrightWhite));

    public static AnsiColor ForegroundRgb(byte r, byte g, byte b) => new($"38;2;{r};{g};{b}", isForeground: true, "RGB foreground");
    public static AnsiColor BackgroundRgb(byte r, byte g, byte b) => new($"48;2;{r};{g};{b}", isForeground: false, "RGB background");

    public override bool Equals(object obj) => Equals(obj as AnsiColor);
    public bool Equals(AnsiColor other) => other != null && Code == other.Code;
    public override int GetHashCode() => Code.GetHashCode();

    public static bool operator ==(AnsiColor left, AnsiColor right) => EqualityComparer<AnsiColor>.Default.Equals(left, right);
    public static bool operator !=(AnsiColor left, AnsiColor right) => !(left == right);

    public override string ToString() => friendlyName;

    public readonly struct PredefinedAnsiColor
    {
        public readonly AnsiColor Foreground;
        public readonly AnsiColor Background;

        public PredefinedAnsiColor(string foregroundCode, string backgroundCode, string name)
        {
            Foreground = new AnsiColor(foregroundCode, isForeground: true, $"{name} foreground");
            Background = new AnsiColor(backgroundCode, isForeground: false, $"{name} background");
        }
    }
}
