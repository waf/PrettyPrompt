#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;

using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt;

public class KeyBindings
{
    public KeyPressPatterns CommitCompletion { get; }
    public KeyPressPatterns TriggerCompletionList { get; }

    public KeyBindings(
        KeyPressPattern[]? commitCompletion = null,
        KeyPressPattern[]? triggerCompletionList = null)
    {
        CommitCompletion = commitCompletion ?? new KeyPressPattern[]
        {
            new(Enter),
            new(Tab)
        };

        TriggerCompletionList = triggerCompletionList ?? new KeyPressPattern[]
        {
            new(Control, Spacebar),
        };
    }

    public readonly struct KeyPressPatterns
    {
        private readonly KeyPressPattern[] definedPatterns;

        public KeyPressPatterns(KeyPressPattern[] definedPatterns)
            => this.definedPatterns = definedPatterns;

        public static implicit operator KeyPressPatterns(KeyPressPattern[] definedPatterns)
            => new(definedPatterns);

        /// <summary>
        /// See <see cref="KeyPress.Pattern"/>.
        /// </summary>
        public bool Matches(object pattern)
        {
            foreach (var definedPattern in definedPatterns)
            {
                if (definedPattern.EqualsObjectPattern(pattern))
                {
                    return true;
                }
            }
            return false;
        }
    }
}