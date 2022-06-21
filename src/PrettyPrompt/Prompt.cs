#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Cancellation;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.History;
using PrettyPrompt.Panes;
using PrettyPrompt.Rendering;
using TextCopy;

namespace PrettyPrompt;

/// <inheritdoc cref="IPrompt" />
public sealed class Prompt : IPrompt, IAsyncDisposable
{
    private readonly IConsole console;
    private readonly HistoryLog history;
    private readonly PromptConfiguration configuration;
    private readonly CancellationManager cancellationManager;
    private readonly IClipboard clipboard;
    private readonly SyntaxHighlighter highlighter;
    private readonly IPromptCallbacks promptCallbacks;
    private Task? savePersistentHistoryTask;

    /// <summary>
    /// Instantiates a prompt object. This object can be re-used for multiple invocations of <see cref="ReadLineAsync()"/>.
    /// </summary>
    /// <param name="persistentHistoryFilepath">The filepath of where to store history entries. If null, persistent history is disabled.</param>
    /// <param name="callbacks">A collection of callbacks for modifying and intercepting the prompt's behavior</param>
    /// <param name="console">The implementation of the console to use. This is mainly for ease of unit testing</param>
    /// <param name="configuration">If null, default configuration is used.</param>
    public Prompt(
        string? persistentHistoryFilepath = null,
        PromptCallbacks? callbacks = null,
        IConsole? console = null,
        PromptConfiguration? configuration = null)
    {
        this.console = console ?? new SystemConsole();
        this.console.InitVirtualTerminalProcessing();

        this.configuration = configuration ?? new PromptConfiguration();
        this.history = new HistoryLog(persistentHistoryFilepath, this.configuration.KeyBindings);
        this.cancellationManager = new CancellationManager(this.console);
        this.clipboard = (console is IConsoleWithClipboard consoleWithClipboard) ? consoleWithClipboard.Clipboard : new Clipboard();

        promptCallbacks = callbacks ?? new PromptCallbacks();
        this.highlighter = new SyntaxHighlighter(promptCallbacks, PromptConfiguration.HasUserOptedOutFromColor);
    }

    /// <inheritdoc cref="IPrompt.ReadLineAsync()" />
    public async Task<PromptResult> ReadLineAsync()
    {
        var renderer = new Renderer(console, configuration);
        renderer.RenderPrompt();

        // code pane contains the code the user is typing. It does not include the prompt (i.e. "> ")
        var codePane = new CodePane(console, configuration, clipboard);

        // completion pane is the pop-up window that shows potential autocompletions.
        var completionPane = new CompletionPane(
            codePane,
            promptCallbacks,
            configuration);

        codePane.Bind(completionPane);

        history.Track(codePane);
        cancellationManager.CaptureControlC();

        foreach (var key in KeyPress.ReadForever(console))
        {
            // grab the code area width every key press, so we rerender appropriately when the console is resized.
            codePane.MeasureConsole();

            await InterpretKeyPress(key, codePane, completionPane, cancellationToken: default).ConfigureAwait(false);

            // typing / word-wrapping may have scrolled the console, giving us more room.
            codePane.MeasureConsole();

            // render the typed input, with syntax highlighting
            var inputText = codePane.Document.GetText();
            var highlights = await highlighter.HighlightAsync(inputText, cancellationToken: default).ConfigureAwait(false);

            // the key press may have caused the prompt to return its input, e.g. <Enter> or a callback.
            var result = await GetResult(codePane, key, inputText, cancellationToken: default).ConfigureAwait(false);

            await renderer.RenderOutput(result, codePane, completionPane, highlights, key, cancellationToken: default).ConfigureAwait(false);

            if (result is not null)
            {
                //wait for potential previous saving
                await (savePersistentHistoryTask ?? Task.CompletedTask).ConfigureAwait(false);

                savePersistentHistoryTask = history.SavePersistentHistoryAsync(inputText);
                cancellationManager.AllowControlCToCancelResult(result);
                return result;
            }
        }

        Debug.Assert(false, "Should never reach here due to infinite " + nameof(KeyPress.ReadForever));
        return null;
    }

    private async Task InterpretKeyPress(KeyPress key, CodePane codePane, CompletionPane completionPane, CancellationToken cancellationToken)
    {
        if (!completionPane.WouldKeyPressCommitCompletionItem(key))
        {
            key = await promptCallbacks.TransformKeyPressAsync(codePane.Document.GetText(), codePane.Document.Caret, key, cancellationToken).ConfigureAwait(false);
        }

        foreach (var panes in new IKeyPressHandler[] { completionPane, history, codePane })
            await panes.OnKeyDown(key, cancellationToken).ConfigureAwait(false);

        foreach (var panes in new IKeyPressHandler[] { completionPane, history, codePane })
            await panes.OnKeyUp(key, cancellationToken).ConfigureAwait(false);

        //we don't support text selection while completion list is open
        //text selection can put completion list into broken state, where filtering does not work
        //so we want this assert to be true
        Debug.Assert(!completionPane.IsOpen || (codePane.Selection is null));
    }

    private async Task<PromptResult?> GetResult(CodePane codePane, KeyPress key, string inputText, CancellationToken cancellationToken)
    {
        // process any user-defined keyboard shortcuts
        if (promptCallbacks.TryGetKeyPressCallbacks(key.ConsoleKeyInfo, out var callback))
        {
            var result = await callback.Invoke(inputText, codePane.Document.Caret, cancellationToken).ConfigureAwait(false);
            if (result is not null)
                return result;
        }
        return codePane.Result;
    }

    /// <summary>
    /// Given a string, and a collection of highlighting instructions, create ANSI Escape Sequence instructions that will 
    /// draw the highlighted text to the console.
    /// </summary>
    /// <param name="text">the text to print</param>
    /// <param name="formatting">the formatting instructions containing color information for the <paramref name="text"/></param>
    /// <param name="textWidth">the width of the console. This controls the word wrapping, and can usually be <see cref="Console.BufferWidth"/>.</param>
    /// <returns>A string of escape sequences that will draw the <paramref name="text"/></returns>
    /// <remarks>
    /// This function is different from most in that it involves drawing _output_ to the screen, rather than
    /// drawing typed user input. It's still useful because if users want syntax-highlighted input, chances are they
    /// also want syntax-highlighted output. It's sort of co-opting existing input functions for the purposes of output.
    /// </remarks>
    public static string RenderAnsiOutput(string text, IReadOnlyCollection<FormatSpan> formatting, int textWidth)
    {
        var rows = CellRenderer.ApplyColorToCharacters(formatting, text, textWidth);
        var initialCursor = ConsoleCoordinate.Zero;
        var finalCursor = new ConsoleCoordinate(rows.Length - 1, 0);
        var output = IncrementalRendering.CalculateDiff(
            previousScreen: new Screen(textWidth, rows.Length, initialCursor),
            currentScreen: new Screen(textWidth, rows.Length, finalCursor, new ScreenArea(initialCursor, rows, TruncateToScreenHeight: false)),
            ansiCoordinate: initialCursor
        );
        return output;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await (savePersistentHistoryTask ?? Task.CompletedTask).ConfigureAwait(false);
    }
}

/// <summary>
/// The main entry point of the prompt functionality.
/// This class should be instantiated once with the desired configuration; then <see cref="ReadLineAsync"/> 
/// can be called once for each line of input.
/// </summary>
/// <remarks>
/// We don't actually use this interface internally, but it's likely that
/// users will want to mock the prompt as it's IO-related.
/// </remarks>
public interface IPrompt
{
    /// <summary>
    /// Prompts the user for input and returns the result.
    /// </summary>
    /// <returns>The input that the user submitted</returns>
    Task<PromptResult> ReadLineAsync();
}

/// <summary>
/// Represents the user's input from the prompt.
/// If the user successfully submitted text, <see cref="IsSuccess"/> will be true and <see cref="Text"/> will contain the input.
/// If the user cancelled (via ctrl-c), <see cref="IsSuccess"/> will be false and <see cref="Text"/> will be an empty string.
/// </summary>
public class PromptResult
{
    public bool IsSuccess { get; }

    /// <summary>
    /// The current input on the prompt when the user submited the prompt or pressed the keybinding.
    /// </summary>
    public string Text { get; }

    public ConsoleKeyInfo SubmitKeyInfo { get; }

    internal CancellationTokenSource? CancellationTokenSource { get; set; }

    /// <summary>
    /// If your user presses ctrl-c while your application is processing the user's input, this CancellationToken will be
    /// signaled (i.e. IsCancellationRequested will be set to true)
    /// </summary>
    public CancellationToken CancellationToken => CancellationTokenSource?.Token ?? CancellationToken.None;

    public PromptResult(bool isSuccess, string text, ConsoleKeyInfo submitKeyInfo)
    {
        IsSuccess = isSuccess;
        Text = text;
        SubmitKeyInfo = submitKeyInfo;
    }
}

/// <summary>
/// Represents the result of a user's key press, when they pressed a keybinding from <see cref="PromptCallbacks.GetKeyPressCallbacks"/>.
/// </summary>
public class KeyPressCallbackResult : PromptResult
{
    /// <summary>
    /// Any output the consuming application wants to display as a result of the keybinding.
    /// </summary>
    public string? Output { get; }

    public KeyPressCallbackResult(string input, string? output)
        : base(isSuccess: true, input, submitKeyInfo: default)
    {
        Output = output;
    }
}