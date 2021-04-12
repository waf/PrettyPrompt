using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.History;
using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrettyPrompt
{
    /// <summary>
    /// The main entry point of the prompt functionality.
    /// This class should be instantiated once with the desired configuration; then <see cref="ReadLineAsync"/> 
    /// can be called once for each line of input.
    /// </summary>
    public class Prompt : IPrompt
    {
        private readonly IConsole console;
        private readonly HistoryLog history;
        private readonly CompletionHandlerAsync completionCallback;
        private readonly HighlightHandlerAsync highlightCallback;
        private readonly ForceSoftEnterHandlerAsync detectSoftEnterCallback;

        /// <summary>
        /// Instantiates a prompt object. This object can be re-used for multiple lines of input.
        /// </summary>
        /// <param name="completionHandler">An optional delegate that provides autocompletion results</param>
        /// <param name="highlightHandler">An optional delegate that controls syntax highlighting</param>
        /// <param name="forceSoftEnterHandler">
        /// An optional delegate that allows for intercepting the "Enter" key and causing it to
        /// insert a "soft enter" (newline) instead of submitting the prompt.
        /// </param>
        /// <param name="console">The implementation of the console to use. This is mainly for ease of unit testing</param>
        public Prompt(
            CompletionHandlerAsync completionHandler = null,
            HighlightHandlerAsync highlightHandler = null,
            ForceSoftEnterHandlerAsync forceSoftEnterHandler = null,
            IConsole console = null)
        {
            this.console = console ?? new SystemConsole();
            this.console.InitVirtualTerminalProcessing();

            this.history = new HistoryLog();
            this.completionCallback = completionHandler ?? ((_, _) => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>()));
            this.highlightCallback = highlightHandler ?? ((_) => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>()));
            this.detectSoftEnterCallback = forceSoftEnterHandler ?? ((_) => Task.FromResult(false));
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
            // completion pane is the pop-up window that shows potential autocompletions.
            var completionPane = new CompletionPane(codePane, completionCallback);

            history.Track(codePane);

            while (true)
            {
                var key = new KeyPress(console.ReadKey(intercept: true));

                // grab the code area width every key press, so we rerender appropriately when the console is resized.
                codePane.CodeAreaWidth = console.BufferWidth - prompt.Length;
                codePane.CodeAreaHeight = console.WindowHeight - codePane.TopCoordinate;

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane, history })
                    await panes.OnKeyDown(key);

                codePane.WordWrap();

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane, history })
                    await panes.OnKeyUp(key);

                var highlights = await highlightCallback.Invoke(codePane.Input.ToString());
                await renderer.RenderOutput(codePane, completionPane, highlights, key);

                if (codePane.Result is not null)
                {
                    return codePane.Result;
                }
            }
        }
    }

    public interface IPrompt
    {
        Task<PromptResult> ReadLineAsync(string prompt);
    }

    /// <summary>
    /// Represents the user's input from the prompt.
    /// If the user successfully submitted text, Success will be true and Text will be present.
    /// If the user cancelled (via ctrl-c), Success will be false and Text will be an empty string.
    /// </summary>
    public record PromptResult(bool Success, string Text);
}
