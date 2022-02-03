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
    public KeyPressPatterns NewLine { get; }
    public KeyPressPatterns SubmitPrompt { get; }

    public KeyBindings(
        KeyPressPattern[]? commitCompletion = null,
        KeyPressPattern[]? triggerCompletionList = null,
        KeyPressPattern[]? newLine = null,
        KeyPressPattern[]? submitPrompt = null)
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

        NewLine = newLine ?? new KeyPressPattern[]
        {
            new(Shift, Enter),
        };

        SubmitPrompt = submitPrompt ?? new KeyPressPattern[]
        {
            new(Enter),
            new(Control, Enter),
            new(Control | Alt, Enter),
        };
    }
}