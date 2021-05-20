using PrettyPrompt.Cancellation;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.History;
using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PrettyPrompt
{
    /// <summary>
    /// The main entry point of the prompt functionality.
    /// This class should be instantiated once with the desired configuration; then <see cref="ReadLineAsync"/> 
    /// can be called once for each line of input.
    /// </summary>
    public sealed class Prompt : IPrompt
    {
        private readonly IConsole console;
        private readonly HistoryLog history;
        private readonly CancellationManager cancellationManager;

        private readonly CompletionCallbackAsync completionCallback;
        private readonly ForceSoftEnterCallbackAsync detectSoftEnterCallback;
        private readonly SyntaxHighlighter highlighter;

        /// <summary>
        /// Instantiates a prompt object. This object can be re-used for multiple lines of input.
        /// </summary>
        /// <param name="persistentHistoryFilepath">The filepath of where to store history entries. If null, persistent history is disabled.</param>
        /// <param name="callbacks">A collection of callbacks for modifying and intercepting the prompt's behavior</param>
        /// <param name="console">The implementation of the console to use. This is mainly for ease of unit testing</param>
        public Prompt(
            string persistentHistoryFilepath = null,
            PromptCallbacks callbacks = null,
            IConsole console = null)
        {
            this.console = console ?? new SystemConsole();
            this.console.InitVirtualTerminalProcessing();

            this.history = new HistoryLog(persistentHistoryFilepath);
            this.cancellationManager = new CancellationManager(this.console);

            this.completionCallback = callbacks?.CompletionCallback ?? ((_, _) => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>()));
            this.detectSoftEnterCallback = callbacks?.ForceSoftEnterCallback ?? ((_) => Task.FromResult(false));

            var highlightCallback = callbacks?.HighlightCallback ?? ((_) => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>()));
            this.highlighter = new SyntaxHighlighter(highlightCallback);
        }

        /// <summary>
        /// Prompts the user for input and returns the result.
        /// </summary>
        /// <param name="prompt">The prompt string to draw (e.g. "> ")</param>
        /// <returns>The input that the user submitted</returns>
        public async Task<PromptResult> ReadLineAsync(string prompt)
        {
            var renderer = new Renderer(console, prompt);
            renderer.RenderPrompt();

            // code pane contains the code the user is typing. It does not include the prompt (i.e. "> ")
            var codePane = new CodePane(topCoordinate: console.CursorTop, detectSoftEnterCallback);
            codePane.MeasureConsole(console, prompt);
            // completion pane is the pop-up window that shows potential autocompletions.
            var completionPane = new CompletionPane(codePane, completionCallback);

            history.Track(codePane);
            cancellationManager.CaptureControlC();

            foreach(var key in KeyPress.ReadForever(console))
            {
                // grab the code area width every key press, so we rerender appropriately when the console is resized.
                codePane.MeasureConsole(console, prompt);

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane, history })
                    await panes.OnKeyDown(key).ConfigureAwait(false);

                codePane.WordWrap();

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane, history })
                    await panes.OnKeyUp(key).ConfigureAwait(false);

                var highlights = await highlighter.HighlightAsync(codePane.Input).ConfigureAwait(false);
                await renderer.RenderOutput(codePane, completionPane, highlights, key).ConfigureAwait(false);

                codePane.MeasureConsole(console, prompt);

                if (codePane.Result is not null)
                {
                    _ = history.SavePersistentHistoryAsync(codePane.Input).ConfigureAwait(false);
                    cancellationManager.AllowControlCToCancelResult(codePane.Result);
                    return codePane.Result;
                }
            }

            Debug.Assert(false, "Should never reach here due to infinite " + nameof(KeyPress.ReadForever));
            return null;
        }
    }

    public interface IPrompt // we don't actually use this interface, but it's likely that users will want to mock the prompt as it's IO related.
    {
        Task<PromptResult> ReadLineAsync(string prompt);
    }

    /// <summary>
    /// Represents the user's input from the prompt.
    /// If the user successfully submitted text, Success will be true and Text will be present.
    /// If the user cancelled (via ctrl-c), Success will be false and Text will be an empty string.
    /// </summary>
    public record PromptResult(bool IsSuccess, string Text, bool IsHardEnter)
    {
        internal CancellationTokenSource CancellationTokenSource { get; set; }
        public CancellationToken CancellationToken => CancellationTokenSource.Token;
    }

    public class PromptCallbacks
    {
        /// <summary>
        /// An optional delegate that provides autocompletion results
        /// </summary>
        public CompletionCallbackAsync CompletionCallback { get; init; }

        /// <summary>
        /// An optional delegate that controls syntax highlighting
        /// </summary>
        public HighlightCallbackAsync HighlightCallback { get; init; }

        /// <summary>
        /// An optional delegate that allows for intercepting the "Enter" key and causing it to
        /// insert a "soft enter" (newline) instead of submitting the prompt.
        /// </summary>
        public ForceSoftEnterCallbackAsync ForceSoftEnterCallback { get; init; }
    }
}
