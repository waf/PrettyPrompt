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
    public KeyPressPatterns HistoryPrevious { get; }
    public KeyPressPatterns HistoryNext { get; }

    public KeyBindings(
        KeyPressPatterns commitCompletion = default,
        KeyPressPatterns triggerCompletionList = default,
        KeyPressPatterns newLine = default,
        KeyPressPatterns submitPrompt = default,
        KeyPressPatterns historyPrevious = default,
        KeyPressPatterns historyNext = default)
    {
        CommitCompletion = Get(commitCompletion, new(Enter), new(Tab));
        TriggerCompletionList = Get(triggerCompletionList, new KeyPressPattern(Control, Spacebar));
        NewLine = Get(newLine, new KeyPressPattern(Shift, Enter));
        SubmitPrompt = Get(submitPrompt, new(Enter), new(Control, Enter), new(Control | Alt, Enter));
        HistoryPrevious = Get(historyPrevious, new KeyPressPattern(UpArrow));
        HistoryNext = Get(historyNext, new KeyPressPattern(DownArrow));

        static KeyPressPatterns Get(KeyPressPatterns patterns, params KeyPressPattern[] defaultPatterns)
            => patterns.HasAny ? patterns : new(defaultPatterns);
}
}