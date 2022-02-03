#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

namespace PrettyPrompt.Consoles;

public readonly struct KeyPressPatterns
{
    private readonly KeyPressPattern[] definedPatterns;

    public KeyPressPatterns(params KeyPressPattern[] definedPatterns)
        => this.definedPatterns = definedPatterns;

    public static implicit operator KeyPressPatterns(KeyPressPattern[] definedPatterns)
        => new(definedPatterns);

    public bool Matches(KeyPressPattern pattern)
    {
        foreach (var definedPattern in definedPatterns)
        {
            if (definedPattern == pattern)
            {
                return true;
            }
        }
        return false;
    }
}