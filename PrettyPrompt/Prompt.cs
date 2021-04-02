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
    public record PromptResult(bool Success, string Text);

    public class Prompt
    {
        private readonly IConsole console;
        private readonly HistoryLog history;
        private readonly CompletionHandlerAsync completionCallback;
        private readonly HighlightHandlerAsync highlightCallback;
        private readonly ForceSoftEnterHandlerAsync detectSoftEnterCallback;

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

        public async Task<PromptResult> ReadLine(string prompt)
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

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane, history })
                    await panes.OnKeyDown(key);

                codePane.WordWrap();

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane, history })
                    await panes.OnKeyUp(key);

                var highlights = await highlightCallback.Invoke(codePane.Input.ToString());
                renderer.RenderOutput(codePane, completionPane, highlights, key);

                if (codePane.Result is not null)
                {
                    return codePane.Result;
                }
            }
        }
    }
}
