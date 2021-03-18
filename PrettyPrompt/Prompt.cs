using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Panes;

namespace PrettyPrompt
{
    public record PromptResult(bool Success, string Text);

    public class Prompt
    {
        private readonly IConsole console;
        private readonly CompletionHandlerAsync complete;
        private readonly HighlightHandlerAsync highlight;

        public Prompt(
            CompletionHandlerAsync completionHandler = null,
            HighlightHandlerAsync highlightHandler = null,
            IConsole console = null)
        {
            this.console = console ?? new SystemConsole();
            this.console.InitVirtualTerminalProcessing();

            this.complete = completionHandler ?? ((_, _) => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>()));
            this.highlight = highlightHandler ?? ((_) => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>()));
        }

        public async Task<PromptResult> ReadLine(string prompt)
        {
            var renderer = new Renderer(console, prompt);
            renderer.RenderPrompt();

            // code pane contains the code the user is typing. It does not include the prompt (i.e. "> ")
            var codePane = new CodePane(topCoordinate: console.CursorTop);
            // completion pane is the pop-up window that shows potential autocompletions.
            var completionPane = new CompletionPane(codePane, complete);

            while (true)
            {
                var key = new KeyPress(console.ReadKey(intercept: true));

                // grab the code area width every key press, so we rerender appropriately when the console is resized.
                codePane.CodeAreaWidth = console.BufferWidth - prompt.Length;

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane })
                    await panes.OnKeyDown(key);

                codePane.WordWrap();

                foreach (var panes in new IKeyPressHandler[] { completionPane, codePane })
                    await panes.OnKeyUp(key);

                var highlights = await highlight.Invoke(codePane.Input.ToString());
                renderer.RenderOutput(codePane, completionPane, highlights, key);

                if (codePane.Result is not null)
                {
                    return codePane.Result;
                }
            }
        }
    }
}
