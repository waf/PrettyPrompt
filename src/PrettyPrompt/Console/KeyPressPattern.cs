#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Diagnostics;

namespace PrettyPrompt.Consoles;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "()}")]
public readonly struct KeyPressPattern
{
    private readonly KeyPressPatternType type;

    public readonly ConsoleModifiers Modifiers;
    public readonly ConsoleKey Key;
    public readonly char Character;

    public KeyPressPattern(ConsoleKey key)
        : this(modifiers: default, key)
    { }

    public KeyPressPattern(ConsoleModifiers modifiers, ConsoleKey key)
    {
        type = KeyPressPatternType.ConsoleKey;
        Modifiers = modifiers;
        Key = key;
        Character = MapToCharacter(key);
    }

    public KeyPressPattern(char character)
    {
        type = KeyPressPatternType.Character;
        Modifiers = default;
        Key = default;
        Character = character;
    }

    public bool Matches(ConsoleKeyInfo keyInfo)
    {
        return
            type == KeyPressPatternType.ConsoleKey ?
            keyInfo.Modifiers == Modifiers && keyInfo.Key == Key :
            (keyInfo.Modifiers is default(ConsoleModifiers) or ConsoleModifiers.Shift) && keyInfo.KeyChar == Character; //Shift is ok, it only determines casing of letter
    }

    private static char MapToCharacter(ConsoleKey key)
    {
        var keyString = key.ToString();
        return keyString switch
        {
            { Length: 1 } => keyString[0],
            "Enter" => '\n',
            "Spacebar" => ' ',
            "Escape" => '\x1b',
            "Tab" => '\t',
            _ => '\0'
        };
    }

    private string GetDebuggerDisplay()
    {
        if (type == KeyPressPatternType.ConsoleKey)
        {
            return Modifiers == default ? $"{Key}" : $"{Modifiers}+{Key}";
        }
        else
        {
            return $"{Character}";
        }
    }
}

public enum KeyPressPatternType
{
    ConsoleKey,
    Character
}