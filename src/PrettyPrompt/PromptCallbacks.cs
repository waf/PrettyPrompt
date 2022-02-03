#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt;

/// <summary>
/// A callback your application can provide to define custom behavior when a key is pressed.
/// <seealso cref="PromptCallbacks.KeyPressCallbacks"/>
/// </summary>
/// <param name="text">The user's input text</param>
/// <param name="caret">The index of the text caret in the input text</param>
/// <returns>
/// <list type="bullet">
/// <item>
/// If a non-null <see cref="KeyPressCallbackResult"/> is returned, the prompt will be submitted.
/// The <see cref="KeyPressCallbackResult"/> will be returned by the current
/// <see cref="Prompt.ReadLineAsync()"/> function.
/// </item>
/// <item>
/// If a null <see cref="KeyPressCallbackResult"/> is returned, then the user will remain on the
/// current prompt.
/// </item>
/// </list>
/// </returns>
public delegate Task<KeyPressCallbackResult?> KeyPressCallbackAsync(string text, int caret);

internal interface IPromptCallbacks
{
    IReadOnlyDictionary<object, KeyPressCallbackAsync> KeyPressCallbacks { get; }
    Task<bool> InterpretKeyPressAsInputSubmit(string text, int caret, KeyPressPattern keyPress);
    Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced);
    Task<TextSpan> GetSpanToReplaceByCompletionkAsync(string text, int caret);
    Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text);
    Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret);
}

public class PromptCallbacks : IPromptCallbacks
{
    protected Dictionary<object, KeyPressCallbackAsync> keyPressCallbacks = new();

    /// <summary>
    /// Determines which part of document will be replaced by inserted completion item.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <returns>Span of text that will be replaced by inserted completion item.</returns>
    async Task<TextSpan> IPromptCallbacks.GetSpanToReplaceByCompletionkAsync(string text, int caret)
    {
        var span = await GetSpanToReplaceByCompletionkAsync(text, caret).ConfigureAwait(false);
        if (!new TextSpan(0, text.Length).Contains(span))
        {
            throw new InvalidOperationException("Resulting TextSpan has to be inside the document.");
        }
        if (!span.Contains(new TextSpan(caret, 0)))
        {
            throw new InvalidOperationException("Resulting TextSpan has to contain current caret position.");
        }
        return span;
    }

    /// <summary>
    /// Determines which part of document will be replaced by inserted completion item.
    /// If not specified, default word detection is used.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <returns>Span of text that will be replaced by inserted completion item.</returns>
    protected virtual Task<TextSpan> GetSpanToReplaceByCompletionkAsync(string text, int caret)
    {
        int wordStart = caret;
        for (int i = wordStart - 1; i >= 0; i--)
        {
            if (IsWordCharacter(text[i]))
            {
                --wordStart;
            }
            else
            {
                break;
            }
        }
        if (wordStart < 0) wordStart = 0;

        int wordEnd = caret;
        for (int i = caret; i < text.Length; i++)
        {
            if (IsWordCharacter(text[i]))
            {
                ++wordEnd;
            }
            else
            {
                break;
            }
        }

        return Task.FromResult(TextSpan.FromBounds(wordStart, wordEnd));

        static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';
    }

    /// <summary>
    /// Provides to auto-completion items for specified position in the input text.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="spanToBeReplaced">Span of text that will be replaced by inserted completion item</param>
    /// <returns>A list of possible completions that will be displayed in the autocomplete menu.</returns>
    public virtual Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced)
        => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>());

    /// <summary>
    /// Controls when the completion window should open.
    /// If not specified, C#-like intellisense style behavior is used.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <returns>
    /// A value indicating whether the completion window should automatically open.
    /// </returns>
    public virtual Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret)
    {
        if (caret > 0 && text[caret - 1] is '.' or '(') // typical "intellisense behavior", opens for new methods and parameters
        {
            return Task.FromResult(true);
        }

        if (caret == 1 && !char.IsWhiteSpace(text[0]) // 1 word character typed in brand new prompt
            && (text.Length == 1 || !char.IsLetterOrDigit(text[1]))) // if there's more than one character on the prompt, but we're typing a new word at the beginning (e.g. "a| bar")
        {
            return Task.FromResult(true);
        }

        // open when we're starting a new "word" in the prompt.
        return Task.FromResult(caret - 2 >= 0 && char.IsWhiteSpace(text[caret - 2]) && char.IsLetter(text[caret - 1]));
    }

    /// <summary>
    /// Provides syntax-highlighting for input text.
    /// </summary>
    /// <param name="text">The text to be highlighted</param>
    /// <returns>A collection of formatting instructions</returns>
    public virtual Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text)
        => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>());

    /// <summary>
    /// Defines whether given <see cref="KeyPressPattern"/> should be interpreted as
    /// the prompt input submit.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="keyPress">Key press pattern in question</param>
    /// <returns>
    /// <see langword="true"/> if the prompt should be submitted or <see langword="false"/> newline should be inserted ("soft-enter").
    /// </returns>
    public virtual Task<bool> InterpretKeyPressAsInputSubmit(string text, int caret, KeyPressPattern keyPress)
        => Task.FromResult(false);

    /// <summary>
    /// A dictionary of "(ConsoleModifiers, ConsoleKey)" to "Callback Functions"
    /// The callback function will be invoked when the keys are pressed, with the current prompt
    /// text and the caret position within the text. ConsoleModifiers can be omitted if not required.
    /// </summary>
    /// <example>
    /// The following will invoke MyOtherCallbackFn whenever Ctrl+F1 is pressed:
    /// <code>
    /// KeyPressCallbacks =
    /// {
    ///     [ConsoleKey.F1] = MyCallbackFn
    ///     [(ConsoleModifiers.Control, ConsoleKey.F1)] = MyOtherCallbackFn
    /// }
    /// </code>
    /// If the prompt should be submitted as a result of the user's key press, then a non-null <see cref="KeyPressCallbackResult"/> may
    /// be returned from the <see cref="KeyPressCallbackAsync"/> function. If a null result is returned, then the user will remain on
    /// the current input prompt.
    /// </example>
    public IReadOnlyDictionary<object, KeyPressCallbackAsync> KeyPressCallbacks => keyPressCallbacks;
}