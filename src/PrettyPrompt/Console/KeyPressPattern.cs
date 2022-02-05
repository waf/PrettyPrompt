#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;

namespace PrettyPrompt.Consoles;

public readonly struct KeyPressPattern : IEquatable<KeyPressPattern>
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

    internal KeyPressPattern(object pattern)
    {
        if (pattern is ConsoleKey key)
        {
            Modifiers = default;
            Key = key;
        }
        else if (pattern is (ConsoleModifiers modifiers, ConsoleKey key2))
        {
            Modifiers = modifiers;
            Key = key2;
        }
        else
        {
            throw new InvalidOperationException("Invalid format of key pattern. It has to be ConsoleKey or ValueTuple<ConsoleModifiers, ConsoleKey>.");
        }
    }

    public static bool operator !=(KeyPressPattern left, KeyPressPattern right) => !(left == right);
    public static bool operator ==(KeyPressPattern left, KeyPressPattern right) => left.Modifiers == right.Modifiers && left.Key == right.Key;

    public static bool operator !=(KeyPressPattern left, ConsoleKey right) => !(left == right);
    public static bool operator ==(KeyPressPattern left, ConsoleKey right) => left == new KeyPressPattern(right);

    public static bool operator !=(KeyPressPattern left, (ConsoleModifiers Modifiers, ConsoleKey Key) right) => !(left == right);
    public static bool operator ==(KeyPressPattern left, (ConsoleModifiers Modifiers, ConsoleKey Key) right) => left == new KeyPressPattern(right.Modifiers, right.Key);

    public bool Equals(KeyPressPattern pattern) => this == pattern;
    public override bool Equals(object? obj) => obj is KeyPressPattern other && this == other;
    public override int GetHashCode() => (Modifiers, Key).GetHashCode();
}