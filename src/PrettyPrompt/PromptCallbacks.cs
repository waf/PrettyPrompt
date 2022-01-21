#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt;

/// <summary>
/// A callback your application can provide to change word detection behaviour used for completion items insertion.
/// <seealso cref="PromptCallbacks.SpanToReplaceByCompletionCallback"/>
/// </summary>
/// <param name="text">The user's input text</param>
/// <param name="caret">The index of the text caret in the input text</param>
/// <returns>Span of text that will be replaced by inserted completion item.</returns>
public delegate Task<TextSpan> SpanToReplaceByCompletionCallbackAsync(string text, int caret);

/// <summary>
/// A callback your application can provide to autocomplete input text.
/// <seealso cref="PromptCallbacks.CompletionCallback"/>
/// </summary>
/// <param name="text">The user's input text</param>
/// <param name="caret">The index of the text caret in the input text</param>
/// <param name="spanToBeReplaced">Span of text that will be replaced by inserted completion item</param>
/// <returns>A list of possible completions that will be displayed in the autocomplete menu.</returns>
public delegate Task<IReadOnlyList<CompletionItem>> CompletionCallbackAsync(string text, int caret, TextSpan spanToBeReplaced);

/// <summary>
/// A callback your application can provide to determine whether or not the completion window
/// should automatically open.
/// <seealso cref="PromptCallbacks.OpenCompletionWindowCallback"/>
/// </summary>
/// <param name="text">The user's input text</param>
/// <param name="caret">The index of the text caret in the input text</param>
/// <returns>
/// A value indicating whether the completion window should automatically open.
/// </returns>
public delegate Task<bool> OpenCompletionWindowCallbackAsync(string text, int caret);

/// <summary>
/// A callback your application can provide to syntax-highlight input text.
/// <seealso cref="PromptCallbacks.HighlightCallback"/>
/// </summary>
/// <param name="text">The text to be highlighted</param>
/// <returns>A collection of formatting instructions</returns>
public delegate Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text);

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

/// <summary>
/// A callback your application can provide to define whether pressing "Enter" should insert a
/// newline ("soft-enter") or if the prompt should be submitted instead.
/// current prompt, or insert a newline instead.
/// <seealso cref="PromptCallbacks.ForceSoftEnterCallback"/>
/// </summary>
/// <param name="text">The current input text on the prompt.</param>
/// <returns>
/// true if a newline should be inserted ("soft-enter") or false if the prompt should be submitted.
/// </returns>
public delegate Task<bool> ForceSoftEnterCallbackAsync(string text);

public class PromptCallbacks
{
    private SpanToReplaceByCompletionCallbackAsync spanToReplaceByCompletionCallback;

    public PromptCallbacks()
    {
        spanToReplaceByCompletionCallback = GetSpanToReplaceByCompletionAsync;
    }

    /// <summary>
    /// An optional delegate that determines which part of document will be replaced by inserted completion item.
    /// If not specified, default word detection is used.
    /// </summary>
    public SpanToReplaceByCompletionCallbackAsync SpanToReplaceByCompletionCallback
    {
        get => spanToReplaceByCompletionCallback;
        init
        {
            spanToReplaceByCompletionCallback =
                async (string text, int caret) =>
                {
                    var span = await value(text, caret);
                    if (!new TextSpan(0, text.Length).Contains(span))
                    {
                        throw new InvalidOperationException("Resulting TextSpan has to be inside the document.");
                    }
                    if (!span.Contains(new TextSpan(caret, 0)))
                    {
                        throw new InvalidOperationException("Resulting TextSpan has to contain current caret position.");
                    }
                    return span;
                };
        }
    }

    /// <summary>
    /// An optional delegate that provides autocompletion results.
    /// </summary>
    public CompletionCallbackAsync CompletionCallback { get; init; } =
        (_, _, _) => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>());

    /// <summary>
    /// An optional delegate that controls when the completion window should open.
    /// If not specified, C#-like intellisense style behavior is used.
    /// </summary>
    public OpenCompletionWindowCallbackAsync? OpenCompletionWindowCallback { get; init; }

    /// <summary>
    /// An optional delegate that controls syntax highlighting
    /// </summary>
    public HighlightCallbackAsync HighlightCallback { get; init; } =
        _ => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>());

    /// <summary>
    /// An optional delegate that allows for intercepting the "Enter" key and causing it to
    /// insert a "soft enter" (newline) instead of submitting the prompt.
    /// </summary>
    public ForceSoftEnterCallbackAsync ForceSoftEnterCallback { get; init; } =
        _ => Task.FromResult(false);

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
    public Dictionary<object, KeyPressCallbackAsync> KeyPressCallbacks { get; init; } = new();

    /// <summary>
    /// Default implementation.
    /// </summary>
    private static Task<TextSpan> GetSpanToReplaceByCompletionAsync(string text, int caret)
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
}