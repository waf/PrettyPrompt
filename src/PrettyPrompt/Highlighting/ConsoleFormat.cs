#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;

namespace PrettyPrompt.Highlighting;

public readonly record struct ConsoleFormat
{
    public static ConsoleFormat None => default;

    private readonly AnsiColor foreground;
    private readonly AnsiColor background;

    public ConsoleFormat(
        AnsiColor Foreground = null,
        AnsiColor Background = null,
        bool Bold = false,
        bool Underline = false,
        bool Inverted = false)
    {
        if (!(Foreground?.IsForeground ?? true)) throw new ArgumentException("not foreground color", nameof(Foreground));
        if (!(Background?.IsBackground ?? true)) throw new ArgumentException("not background color", nameof(Background));

        this.foreground = Foreground;
        this.background = Background;
        this.Bold = Bold;
        this.Underline = Underline;
        this.Inverted = Inverted;
    }

    public AnsiColor Foreground
    {
        get => foreground;
        init
        {
            if (!(value?.IsForeground ?? true)) throw new ArgumentException("not foreground color", nameof(Foreground));
            foreground = value;
        }
    }

    public AnsiColor Background
    {
        get => background;
        init
        {
            if (!(value?.IsBackground ?? true)) throw new ArgumentException("not background color", nameof(Background));
            background = value;
        }
    }

    public bool Bold { get; init; }
    public bool Underline { get; init; }
    public bool Inverted { get; init; }
}