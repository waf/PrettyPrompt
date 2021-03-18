using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static PrettyPrompt.AnsiEscapeCodes;

namespace PrettyPrompt
{
    public delegate Task<IReadOnlyList<Completion>> CompletionHandlerAsync(string text, int caret);
    public delegate Task<IReadOnlyCollection<FormatSpan>> HighlightHandlerAsync(string text);
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

            this.complete = completionHandler ?? ((_, _) => Task.FromResult<IReadOnlyList<Completion>>(Array.Empty<Completion>()));
            this.highlight = highlightHandler ?? ((_) => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>()));
        }

        public async Task<PromptResult> ReadLine(string prompt)
        {
            console.Write(MoveCursorToColumn(1) + prompt);

            // code pane contains the code the user is typing. It does not include the prompt (i.e. "> ")
            CodePane codePane = new CodePane(console);
            // completion pane is the pop-up window that shows potential autocompletions.
            CompletionPane completionPane = new CompletionPane(codePane, complete);

            while (true)
            {
                var key = new KeyPress(console.ReadKey(intercept: true));

                codePane.CodeAreaWidth = console.BufferWidth - prompt.Length;

                foreach (var keyHandler in new IKeyPressHandler[] { completionPane, codePane })
                    await keyHandler.OnKeyDown(key);

                codePane.WordWrap();

                foreach (var keyHandler in new IKeyPressHandler[] { completionPane, codePane })
                    await keyHandler.OnKeyUp(key);

                if (codePane.Result is not null)
                {
                    return codePane.Result;
                }

                await RenderOutput(prompt, codePane, completionPane);
            }
        }

        private async Task RenderOutput(string prompt, CodePane codePane, CompletionPane completionPane)
        {
            var highlights = await this.highlight.Invoke(codePane.Input.ToString());

            int finalCursorRow = codePane.TopCoordinate + codePane.Cursor.Row;
            int finalCursorColumn = prompt.Length + 1 + codePane.Cursor.Column; // ansi escape sequence column values are 1-indexed.

            console.HideCursor();
            console.Write(
                MoveCursorToPosition(codePane.TopCoordinate, 1)
                   + ClearToEndOfScreen
                   + string.Concat(codePane.WordWrappedLines.Select((line, n) => DrawPrompt(prompt, n) + SyntaxHighlighting.ApplyHighlighting(highlights, line))).EnvironmentNewlines()
                   + completionPane.RenderCompletionMenu(prompt.Length, finalCursorRow, finalCursorColumn)
                   + MoveCursorToPosition(finalCursorRow, finalCursorColumn)
            );
            console.ShowCursor();
        }

        private static string DrawPrompt(string prompt, int n) =>
            n == 0 ? prompt : new string(' ', prompt.Length);
    }
}
