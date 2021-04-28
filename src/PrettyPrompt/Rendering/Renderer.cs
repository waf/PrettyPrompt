using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Panes;
using PrettyPrompt.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt
{
    class Renderer
    {
        private readonly IConsole console;
        private readonly string prompt;

        private Screen previouslyRenderedScreen;

        public Renderer(IConsole console, string prompt)
        {
            this.console = console;
            this.prompt = prompt;
            this.previouslyRenderedScreen = new Screen(0, 0, new ConsoleCoordinate(0, 0));
        }

        public void RenderPrompt()
        {
            console.Write(MoveCursorToColumn(1) + ResetFormatting + prompt);
        }

        public async Task RenderOutput(CodePane codePane, CompletionPane completionPane, IReadOnlyCollection<FormatSpan> highlights, KeyPress key)
        {
            if (codePane.Result is not null)
            {
                Write(
                    MoveCursorDown(codePane.WordWrappedLines.Count - codePane.Cursor.Row - 1)
                    + MoveCursorToColumn(1)
                    + "\n"
                    + ClearToEndOfScreen,
                    hideCursor: true
                );
                return;
            }
            if (key.Pattern is (Control, L))
            {
                previouslyRenderedScreen = new Screen(0, 0, new ConsoleCoordinate(0, 0));
                console.Clear(); // for some reason, using escape codes (ClearEntireScreen and MoveCursorToPosition) leaves
                                 // CursorTop in an old (cached?) state. Using Console.Clear() works around this.
                RenderPrompt();
            }

            // convert our "view models" into characters, contained in screen areas
            ScreenArea codeWidget = BuildCodeScreenArea(codePane, highlights);
            ScreenArea[] completionWidgets = await BuildCompletionScreenAreas(
                completionPane,
                cursor: codePane.Cursor,
                codeAreaStartColumn: prompt.Length,
                codeAreaWidth: codePane.CodeAreaWidth
            );

            // ansi escape sequence row/column values are 1-indexed.
            var ansiCoordinate = new ConsoleCoordinate
            (
                row: 1 + codePane.TopCoordinate,
                column: 1 + prompt.Length
            );

            // draw screen areas to screen representation.
            // later screen areas can overlap earlier screen areas.
            var screen = new Screen(
                codePane.CodeAreaWidth, codePane.CodeAreaHeight, codePane.Cursor, screenAreas: new[] { codeWidget }.Concat(completionWidgets).ToArray()
            );

            if (DidCodeAreaResize(previouslyRenderedScreen, screen))
            {
                previouslyRenderedScreen = previouslyRenderedScreen.Resize(screen.Width, screen.Height);
            }

            // calculate the diff between the previous screen and the
            // screen to be drawn, and output that diff.
            string outputDiff = IncrementalRendering.RenderDiff(screen, previouslyRenderedScreen, ansiCoordinate, codePane.Cursor);
            previouslyRenderedScreen = screen;

            Write(outputDiff, outputDiff.Length > 64);
        }

        private void Write(string output, bool hideCursor = false)
        {
            // rough heuristic. HideCursor() is surprisingly slow, don't use it unless we're rendering something large.
            // the issue mainly shows when e.g. repeating characters by holding down a key (e.g. spacebar)
            if (hideCursor) console.HideCursor();
            console.Write(output);
            if (hideCursor) console.ShowCursor();
        }

        private static bool DidCodeAreaResize(Screen previousScreen, Screen currentScreen) =>
            previousScreen != null && previousScreen?.Width != currentScreen.Width;

        private static ScreenArea BuildCodeScreenArea(CodePane codePane, IReadOnlyCollection<FormatSpan> highlights)
        {
            var highlightedLines = SyntaxHighlighting.ApplyColorToCharacters(highlights, codePane.WordWrappedLines);
            var codeWidget = new ScreenArea(new ConsoleCoordinate(0, 0), highlightedLines, TruncateToScreenHeight: false);
            return codeWidget;
        }

        private async Task<ScreenArea[]> BuildCompletionScreenAreas(CompletionPane completionPane, ConsoleCoordinate cursor, int codeAreaStartColumn, int codeAreaWidth)
        {
            //  _  <-- cursor location
            //  ┌──────────────┐
            //  │ completion 1 │ documentation box with some
            //  │ completion 2 │ docs that may wrap.
            //  │ completion 3 │ 
            //  └──────────────┘

            if (!completionPane.IsOpen)
                return Array.Empty<ScreenArea>();

            if (completionPane.FilteredView.Count == 0)
                return Array.Empty<ScreenArea>();

            int wordWidth = completionPane.FilteredView.Max(w => w.ReplacementText.Length);
            int boxWidth = wordWidth + 2 + 2; // two border characters, plus two spaces for padding

            var completionStart = new ConsoleCoordinate(
                row: cursor.Row + 1,
                column: boxWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                    : cursor.Column + boxWidth >= codeAreaWidth ? codeAreaWidth - boxWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                    : cursor.Column // enough room, we'll show the completion box offset at the cursor location.
            );
            var completionRows = BuildCompletionRows(completionPane, codeAreaWidth, wordWidth, completionStart);

            var documentationStart = new ConsoleCoordinate(cursor.Row + 2, completionStart.Column + boxWidth);
            var selectedItemDescription = await (completionPane.FilteredView.SelectedItem.ExtendedDescription?.Value ?? Task.FromResult(""));
            var documentationRows = BuildDocumentationRows(
                documentation: selectedItemDescription,
                maxWidth: codeAreaWidth - completionStart.Column - boxWidth
            );

            return new[]
            {
                new ScreenArea(completionStart, completionRows),
                new ScreenArea(documentationStart, documentationRows)
            };
        }

        private Row[] BuildCompletionRows(CompletionPane completionPane, int codeAreaWidth, int wordWidth, ConsoleCoordinate completionBoxStart)
        {
            string horizontalBorder = TruncateToWindow(new string('─', wordWidth + 2), 2);
            var selectedItem = completionPane.FilteredView.SelectedItem;

            return completionPane.FilteredView
                .Select((completion, index) =>
                {
                    string leftBorder = "│" + (selectedItem == completion ? "|" : " ");
                    string rightBorder = " │";
                    return new Row(Cell
                        .FromText(leftBorder, Blue)
                        .Concat(Cell.FromText(TruncateToWindow(completion.ReplacementText.PadRight(wordWidth), 4)))
                        .Concat(Cell.FromText(rightBorder, Blue))
                        .ToArray()
                    );
                })
                .Prepend(new Row(Cell.FromText("┌" + horizontalBorder + "┐", Blue)))
                .Append (new Row(Cell.FromText("└" + horizontalBorder + "┘", Blue)))
                .ToArray();

            string TruncateToWindow(string line, int offset) =>
                line.Substring(0, Math.Min(line.Length, codeAreaWidth - completionBoxStart.Column - offset));
        }

        private static Row[] BuildDocumentationRows(string documentation, int maxWidth)
        {
            if (string.IsNullOrEmpty(documentation) || maxWidth < 12)
                return Array.Empty<Row>();

            var width = Math.Min(maxWidth, 52);
            var wrapped = WordWrapping.Wrap(new StringBuilder(documentation).Replace("\r\n", "\n"), 0, width);

            return wrapped.WrappedLines
                .Select(line =>
                    new Row(Cell.FromText(
                        line.Content.Trim().PadRight(width),
                        new ConsoleFormat(foreground: AnsiColor.White, background: AnsiColor.Cyan)
                    ))
                )
                .ToArray();
        }
    }
}
