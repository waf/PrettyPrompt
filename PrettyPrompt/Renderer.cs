using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.Linq;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt
{
    class Renderer
    {
        private readonly IConsole console;
        private readonly string prompt;

        private string previousRenderCache;

        public Renderer(IConsole console, string prompt)
        {
            this.console = console;
            this.prompt = prompt;
        }

        public void RenderPrompt() => console.Write(MoveCursorToColumn(1) + prompt);

        public void RenderOutput(CodePane codePane, CompletionPane completionPane, IReadOnlyCollection<FormatSpan> highlights, KeyPress key)
        {
            if (codePane.Result is not null)
            {
                console.Write("\n" + MoveCursorToColumn(1) + ClearToEndOfScreen);
                return;
            }
            else if (key.Pattern is (Control, L))
            {
                this.previousRenderCache = null;
                console.Clear(); // for some reason, using escape codes (ClearEntireScreen and MoveCursorToPosition) leaves
                                 // CursorTop in an old (cached?) state. Using Console.Clear() works around this.
            }

            // ansi escape sequence row/column values are 1-indexed.
            var cursorAnsiCoordinate = new ConsoleCoordinate
            {
                Row = 1 + codePane.TopCoordinate + codePane.Cursor.Row,
                Column = 1 + prompt.Length + codePane.Cursor.Column,
            };

            string rendered =
                MoveCursorToPosition(1 + codePane.TopCoordinate, 1)
                   + ClearToEndOfScreen
                   + string.Concat(codePane.WordWrappedLines.Select((line, n) => DrawPrompt(prompt, n) + SyntaxHighlighting.ApplyHighlighting(highlights, line))).EnvironmentNewlines()
                   + RenderCompletionMenuAtCursor(completionPane, cursorAnsiCoordinate, prompt.Length, codePane.CodeAreaWidth);

            RenderCached(cursorAnsiCoordinate, rendered);
        }

        private void RenderCached(ConsoleCoordinate cursorAnsiCoordinate, string rendered)
        {
            if (this.previousRenderCache == rendered)
            {
                // optimize for the case where the user is holding down navigation keys like the arrow keys.
                // we don't want to constantly rerender in that case; there can be noticeable input lag.
                console.Write(MoveCursorToPosition(cursorAnsiCoordinate));
            }
            else
            {
                console.HideCursor();
                console.Write(rendered + MoveCursorToPosition(cursorAnsiCoordinate));
                console.ShowCursor();
            }

            this.previousRenderCache = rendered;
        }

        private static string DrawPrompt(string prompt, int n) =>
            n == 0 ? prompt : new string(' ', prompt.Length);

        private static string RenderCompletionMenuAtCursor(CompletionPane completionPane, ConsoleCoordinate cursor, int codeAreaStartColumn, int codeAreaWidth)
        {
            //  _  <-- cursor location
            //  ┌──────────────┐
            //  │ completion 1 │
            //  │ completion 2 │
            //  └──────────────┘

            if (!completionPane.IsOpen)
                return string.Empty;

            if (completionPane.FilteredView.Count == 0)
                return string.Empty;

            int wordWidth = completionPane.FilteredView.Max(w => w.ReplacementText.Length);
            int boxWidth = wordWidth + 2 + 2; // two border characters, plus two spaces for padding
            int boxHeight = completionPane.FilteredView.Count + 2; // two border characters

            int boxStart =
                boxWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                : cursor.Column + boxWidth >= codeAreaWidth ? codeAreaWidth - boxWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                : cursor.Column; // enough room, we'll show the completion box offset at the cursor location.

            return Blue
                + MoveCursorToPosition(cursor.Row + 1, boxStart)
                + "┌" + TruncateToWindow(new string('─', wordWidth + 2), 2) + "┐" + MoveCursorDown(1) + MoveCursorToColumn(boxStart)
                + string.Concat(completionPane.FilteredView.Select(completion => RenderRow(completion, boxStart, wordWidth)))
                + "└" + TruncateToWindow(new string('─', wordWidth + 2), 2) + "┘" + MoveCursorUp(boxHeight) + MoveCursorToColumn(boxStart)
                + ResetFormatting;

            string TruncateToWindow(string line, int offset) =>
                line.Substring(0, Math.Min(line.Length, codeAreaWidth - boxStart - offset));

            string RenderRow(CompletionItem completion, int boxStart, int wordWidth)
            {
                string leftBorder = "│" + (completionPane.SelectedItem?.Value == completion ? "|" : " ");
                string itemText = TruncateToWindow(completion.ReplacementText.PadRight(wordWidth), 4);
                string rightBorder = Blue + " │";
                string nextRowSequence = MoveCursorDown(1) + MoveCursorToColumn(boxStart);

                return leftBorder + ResetFormatting + itemText + rightBorder + nextRowSequence;
            }
        }
    }
}
