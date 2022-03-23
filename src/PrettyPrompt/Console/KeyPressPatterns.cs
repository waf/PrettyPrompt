#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Immutable;

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
        foreach (var pattern in definedPatterns)
        {
            if (pattern.Matches(keyInfo))
            {
                return true;
            }
        }
        return false;
    }

    public bool Matches(ConsoleKeyInfo keyInfo, ImmutableArray<CharacterSetModificationRule> modificationRules)
    {
        foreach (var rule in modificationRules)
        {
            switch (rule.Kind)
            {
                case CharacterSetModificationKind.Add:
                    if (rule.Characters.Contains(keyInfo.KeyChar))
                        return true;
                    continue;
                case CharacterSetModificationKind.Remove:
                    if (rule.Characters.Contains(keyInfo.KeyChar))
                        return false;
                    continue;
                case CharacterSetModificationKind.Replace:
                    return rule.Characters.Contains(keyInfo.KeyChar);
            }
        }

        return Matches(keyInfo);
    }
}