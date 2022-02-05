#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;

namespace PrettyPrompt.Consoles;

public readonly struct KeyPressPatterns
{
    private readonly KeyPressPattern[]? definedPatterns;

    public bool HasAny => definedPatterns?.Length > 0;

    public KeyPressPatterns(params KeyPressPattern[]? definedPatterns)
        => this.definedPatterns = definedPatterns;

    public static implicit operator KeyPressPatterns(KeyPressPattern[]? definedPatterns)
        => new(definedPatterns);

    public bool Matches(ConsoleKeyInfo keyInfo)
    {
        if (definedPatterns is null) return false;
        foreach (var definedPattern in definedPatterns)
        {
            if (definedPattern.Matches(keyInfo))
            {
                return true;
            }
        }
        return false;
    }
}