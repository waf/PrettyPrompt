using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Panes;
using PrettyPrompt.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt
{
    /// <summary>
    /// Given our panes, actually draw them to the screen.
    /// This class mostly deals with generating Cells, which the <see cref="IncrementalRendering"/> class then processes
    /// to generate the minimal set of ANSI escape sequences to write to the screen.
    /// </summary>
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
            // write some newlines to ensure we have enough room to render the completion pane.
            console.Write("\n\n\n" + MoveCursorUp(3) + MoveCursorToColumn(1) + Reset + prompt);
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
            ).ConfigureAwait(false);

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
            var highlightedLines = HighlightRenderer.ApplyColorToCharacters(highlights, codePane.WordWrappedLines);
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

            int wordWidth = completionPane.FilteredView.Max(w => (w.DisplayText ?? w.ReplacementText).Length);
            int boxWidth = wordWidth + 2 + 2; // two border characters, plus two spaces for padding

            var completionStart = new ConsoleCoordinate(
                row: cursor.Row + 1,
                column: boxWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                    : cursor.Column + boxWidth >= codeAreaWidth ? codeAreaWidth - boxWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                    : cursor.Column // enough room, we'll show the completion box offset at the cursor location.
            );
            var completionRows = BuildCompletionRows(completionPane, codeAreaWidth, wordWidth, completionStart);

            var documentationStart = new ConsoleCoordinate(cursor.Row + 1, completionStart.Column + boxWidth);
            var selectedItemDescription = await (
                completionPane.FilteredView.SelectedItem.ExtendedDescription?.Value ?? Task.FromResult("")
            ).ConfigureAwait(false);
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

        private static ConsoleFormat CompletionBorderColor = new ConsoleFormat(Foreground: AnsiColor.Blue);
        private static ConsoleFormat DocumentationBorderColor = new ConsoleFormat(Foreground: AnsiColor.Cyan);

        private Row[] BuildCompletionRows(CompletionPane completionPane, int codeAreaWidth, int wordWidth, ConsoleCoordinate completionBoxStart)
        {
            string horizontalBorder = TruncateToWindow(new string(Box.EdgeHorizontal, wordWidth + 2), 2);

            var selectedItem = completionPane.FilteredView.SelectedItem;

            return completionPane.FilteredView
                .Select((completion, index) =>
                {
                    string leftBorder = Box.EdgeVertical + (selectedItem == completion ? "|" : " ");
                    string rightBorder = " " + Box.EdgeVertical;
                    return new Row(Cell
                        .FromText(leftBorder, CompletionBorderColor)
                        .Concat(Cell.FromText(TruncateToWindow((completion.DisplayText ?? completion.ReplacementText).PadRight(wordWidth), 4)))
                        .Concat(Cell.FromText(rightBorder, CompletionBorderColor))
                        .ToArray()
                    );
                })
                .Prepend(new Row(Cell.FromText(Box.CornerUpperLeft + horizontalBorder + Box.CornerUpperRight, CompletionBorderColor)))
                .Append(new Row(Cell.FromText(Box.CornerLowerLeft + horizontalBorder + Box.CornerLowerRight, CompletionBorderColor)))
                .ToArray();

            string TruncateToWindow(string line, int offset) =>
                line.Substring(0, Math.Min(line.Length, codeAreaWidth - completionBoxStart.Column - offset));
        }

        private static Row[] BuildDocumentationRows(string documentation, int maxWidth)
        {
            if (string.IsNullOrEmpty(documentation) || maxWidth < 12)
                return Array.Empty<Row>();

            // request word wrapping. actual line lengths won't be exactly the requested width due to wrapping.
            var requestedBoxWidth = Math.Min(maxWidth, 55);
            var requestedTextWidth = requestedBoxWidth - 3; // 3 because of left padding, right padding, right border
            var wrapped = WordWrapping.WrapWords(documentation.Replace("\r\n", "\n"), requestedTextWidth);
            var actualTextWidth = wrapped.Select(w => w.Length).Max();
            var actualBoxWidth = actualTextWidth + 3;

            var (boxTop, boxBottom) = Box.HorizontalBorders(actualBoxWidth - 1, leftCorner: false);

            return wrapped
                .Select(line =>
                    new Row(Cell
                        .FromText(" " + line.Trim().PadRight(actualTextWidth))
                        .Concat(Cell.FromText(" " + Box.EdgeVertical, DocumentationBorderColor))
                        .ToArray()
                    )
                )
                .Prepend(new Row(Cell.FromText(boxTop, DocumentationBorderColor)))
                .Append(new Row(Cell.FromText(boxBottom, DocumentationBorderColor)))
                .ToArray();
        }
    }
}
