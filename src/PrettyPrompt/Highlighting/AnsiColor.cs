#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using PrettyPrompt.Consoles;
using static PrettyPrompt.Highlighting.FormattedString.TextElementsEnumerator;

namespace PrettyPrompt.Highlighting;

/// <summary>
/// ANSI color definitions for the terminal.
/// Each color has a different code depending on if it's applied as a foreground or background color.
/// </summary>
/// <remarks>https://en.wikipedia.org/wiki/ANSI_escape_code#Colors</remarks>
public readonly struct AnsiColor : IEquatable<AnsiColor>
{
    private readonly string foregroundCode;
    private readonly string backgroundCode;
    private readonly string friendlyName;

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

    private static readonly Dictionary<string, AnsiColor> ansiColorNames =
        typeof(AnsiColor)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(AnsiColor))
            .ToDictionary(f => f.Name, f => (AnsiColor)f.GetValue(null)!, StringComparer.OrdinalIgnoreCase);

    public static AnsiColor Rgb(byte r, byte g, byte b)
        => new($"38;2;{r};{g};{b}", $"48;2;{r};{g};{b}", $"#{r:X2}{g:X2}{b:X2}");

    public AnsiColor(string foregroundCode, string backgroundCode, string friendlyName)
    {
        this.foregroundCode = foregroundCode;
        this.backgroundCode = backgroundCode;
        this.friendlyName = friendlyName;
    }

    public string GetEscapeSequence(Type type = Type.Foreground) => AnsiEscapeCodes.ToAnsiEscapeSequence(GetCode(type));
    internal string GetCode(Type type = Type.Foreground) => type == Type.Foreground ? foregroundCode : backgroundCode;

    public override bool Equals(object? obj) => obj is AnsiColor other && Equals(other);
    public bool Equals(AnsiColor other) => foregroundCode == other.foregroundCode && backgroundCode == other.backgroundCode;
    public override int GetHashCode() => foregroundCode.GetHashCode();

    public static bool operator ==(AnsiColor left, AnsiColor right) => EqualityComparer<AnsiColor>.Default.Equals(left, right);
    public static bool operator !=(AnsiColor left, AnsiColor right) => !(left == right);

    public override string ToString() => friendlyName;

    public static bool TryParse(string input, out AnsiColor result)
    {
        if (PromptConfiguration.HasUserOptedOutFromColor)
        {
            result = White;
            return true;
        }

        var span = input.AsSpan();
        if (input.StartsWith('#') && span.Length == 7 &&
            byte.TryParse(span.Slice(1, 2), NumberStyles.AllowHexSpecifier, null, out byte r) &&
            byte.TryParse(span.Slice(3, 2), NumberStyles.AllowHexSpecifier, null, out byte g) &&
            byte.TryParse(span.Slice(5, 2), NumberStyles.AllowHexSpecifier, null, out byte b))
        {
            result = Rgb(r, g, b);
            return true;
        }

        if (ansiColorNames.TryGetValue(input, out var color))
        {
            result = color;
            return true;
        }

        result = default;
        return false;
    }

    public enum Type
    {
        Foreground,
        Background
    }
}