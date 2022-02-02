#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;

namespace PrettyPrompt.Consoles;

public readonly struct KeyPressPattern
{
    public readonly ConsoleModifiers Modifiers;
    public readonly ConsoleKey Key;

    public KeyPressPattern(ConsoleKey key)
    {
        Modifiers = default;
        Key = key;
    }

    public KeyPressPattern(ConsoleModifiers modifiers, ConsoleKey key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    /// <summary>
    /// See <see cref="KeyPress.Pattern"/>.
    /// </summary>
    public bool EqualsObjectPattern(object? pattern) => pattern switch
    {
        ConsoleKey key => Modifiers == default && Key == key,
        (ConsoleModifiers modifiers, ConsoleKey key) => Modifiers == modifiers && Key == key,
        _ => throw new InvalidOperationException("invalid format of key pattern"),
    };
}