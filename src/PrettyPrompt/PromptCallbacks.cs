#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt
{
    public delegate Task<IReadOnlyList<CompletionItem>> CompletionCallbackAsync(string text, int caret);
    public delegate Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text);
    public delegate Task KeyPressCallbackAsync(string text, int caret);
    public delegate Task<bool> ForceSoftEnterCallbackAsync(string text);

    public class PromptCallbacks
    {
        /// <summary>
        /// An optional delegate that provides autocompletion results
        /// </summary>
        public CompletionCallbackAsync CompletionCallback { get; init; } = (_, _) => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>());

        /// <summary>
        /// An optional delegate that controls syntax highlighting
        /// </summary>
        public HighlightCallbackAsync HighlightCallback { get; init; } = _ => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>());

        /// <summary>
        /// An optional delegate that allows for intercepting the "Enter" key and causing it to
        /// insert a "soft enter" (newline) instead of submitting the prompt.
        /// </summary>
        public ForceSoftEnterCallbackAsync ForceSoftEnterCallback { get; init; } = _ => Task.FromResult(false);

        /// <summary>
        /// A dictionary of "(ConsoleModifiers, ConsoleKey)" to "Callback Functions"
        /// The callback function will be invoked when the keys are pressed, with the current prompt
        /// text and the caret position within the text. ConsoleModifiers can be omitted if not required.
        /// </summary>
        /// <example>
        /// The following will invoke MyCallbackFn whenever Ctrl+F1 is pressed:
        /// <code>
        /// KeyPressCallbacks =
        /// {
        ///     [(ConsoleModifiers.Control, ConsoleKey.F1)] = MyCallbackFn
        /// }
        /// </code>
        /// </example>
        public Dictionary<object, KeyPressCallbackAsync> KeyPressCallbacks { get; init; } = new Dictionary<object, KeyPressCallbackAsync>();
    }
}

