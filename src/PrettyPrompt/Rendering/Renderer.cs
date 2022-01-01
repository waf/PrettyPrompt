#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Panes;
using PrettyPrompt.Rendering;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;

namespace PrettyPrompt;

/// <summary>
/// Given our panes, actually draw them to the screen.
/// This class mostly deals with generating Cells, which the <see cref="IncrementalRendering"/> class then processes
/// to generate the minimal set of ANSI escape sequences to write to the screen.
/// </summary>
internal class Renderer
{
    private readonly IConsole console;
    private readonly PromptTheme theme;

    private Screen previouslyRenderedScreen = new(0, 0, ConsoleCoordinate.Zero);
    private bool wasTextSelectedDuringPreviousRender;

    public Renderer(IConsole console, PromptTheme theme)
    {
        this.console = console;
        this.theme = theme;
    }

    public void RenderPrompt()
    {
        // write some newlines to ensure we have enough room to render the completion pane.
        var min = CompletionPane.VerticalBordersHeight + CompletionPane.MinCompletionItemsCount;
        var max = CompletionPane.VerticalBordersHeight + CompletionPane.MaxCompletionItemsCount;
        var newLinesCount = (2 * console.WindowHeight / 5).Clamp(min, max);
        console.Write(new string('\n', newLinesCount) + MoveCursorUp(newLinesCount) + MoveCursorToColumn(1) + Reset + theme.Prompt);
    }

    public async Task RenderOutput(PromptResult result, CodePane codePane, CompletionPane completionPane, IReadOnlyCollection<FormatSpan> highlights, KeyPress key)
    {
        if (result is not null)
        {
            if (wasTextSelectedDuringPreviousRender && codePane.Document.Selection is null)
            {
                await Redraw();
            }

            if (completionPane.IsOpen)
            {
                completionPane.IsOpen = false;
                await Redraw();
            }

            Write(
                MoveCursorDown(codePane.Document.WordWrappedLines.Count - codePane.Document.Cursor.Row - 1)
                + MoveCursorToColumn(1)
                + "\n"
                + ClearToEndOfScreen,
                hideCursor: true
            );
        }
        else
        {
            if (key.Pattern is (Control, L))
            {
                previouslyRenderedScreen = new Screen(0, 0, ConsoleCoordinate.Zero);
                console.Clear(); // for some reason, using escape codes (ClearEntireScreen and MoveCursorToPosition) leaves
                                 // CursorTop in an old (cached?) state. Using Console.Clear() works around this.
                RenderPrompt();
                codePane.MeasureConsole(console, theme.Prompt); // our code pane will have more room to render, it now renders at the top of the screen.
            }

            await Redraw();
        }

        wasTextSelectedDuringPreviousRender = codePane.Document.Selection.HasValue;

        async Task Redraw()
        {
            // convert our "view models" into characters, contained in screen areas
            ScreenArea codeWidget = BuildCodeScreenArea(codePane, highlights);
            ScreenArea[] completionWidgets = await BuildCompletionScreenAreas(
                completionPane,
                cursor: codePane.Document.Cursor,
                codeAreaStartColumn: theme.Prompt.Length,
                codeAreaWidth: codePane.CodeAreaWidth
            ).ConfigureAwait(false);

            // ansi escape sequence row/column values are 1-indexed.
            var ansiCoordinate = new ConsoleCoordinate
            (
                row: 1 + codePane.TopCoordinate,
                column: 1 + theme.Prompt.Length
            );

            // draw screen areas to screen representation.
            // later screen areas can overlap earlier screen areas.
            var screen = new Screen(
                codePane.CodeAreaWidth, codePane.CodeAreaHeight, codePane.Document.Cursor, screenAreas: new[] { codeWidget }.Concat(completionWidgets).ToArray()
            );

            if (DidCodeAreaResize(previouslyRenderedScreen, screen))
            {
                previouslyRenderedScreen = previouslyRenderedScreen.Resize(screen.Width, screen.Height);
            }

            // calculate the diff between the previous screen and the
            // screen to be drawn, and output that diff.
            string outputDiff = IncrementalRendering.CalculateDiff(screen, previouslyRenderedScreen, ansiCoordinate);
            previouslyRenderedScreen = screen;

            Write(outputDiff, outputDiff.Length > 64);
        }
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
        var highlightedLines = CellRenderer.ApplyColorToCharacters(highlights, codePane.Document.WordWrappedLines, codePane.Document.Selection);
        // if we've filled up the full line, add a new line at the end so we can render our cursor on this new line.
        if (highlightedLines[^1].Cells.Count > 0
            && (highlightedLines[^1].Cells.Count >= codePane.CodeAreaWidth
                || highlightedLines[^1].Cells[^1]?.Text == "\n"))
        {
            Array.Resize(ref highlightedLines, highlightedLines.Length + 1);
            highlightedLines[^1] = new Row(new List<Cell>());
        }
        var codeWidget = new ScreenArea(ConsoleCoordinate.Zero, highlightedLines, TruncateToScreenHeight: false);
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

        int wordWidth = completionPane.FilteredView
            .Max(w => UnicodeWidth.GetWidth(w.DisplayText.Text ?? w.ReplacementText));
        int boxWidth = wordWidth + 3 + theme.SelectedCompletionItemMarker.Length; // 3 = left border + right border + space before right border

        var completionStart = new ConsoleCoordinate(
            row: cursor.Row + 1,
            column: boxWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                : cursor.Column + boxWidth >= codeAreaWidth ? codeAreaWidth - boxWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                : cursor.Column // enough room, we'll show the completion box offset at the cursor location.
        );
        var completionRows = BuildCompletionRows(completionPane, codeAreaWidth, wordWidth, completionStart);

        var documentationStart = new ConsoleCoordinate(cursor.Row + 1, completionStart.Column + boxWidth);
        var selectedItemDescription = await (
            completionPane.FilteredView.SelectedItem.ExtendedDescription?.Value ?? Task.FromResult(FormattedString.Empty)
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

    private Row[] BuildCompletionRows(CompletionPane completionPane, int codeAreaWidth, int wordWidth, ConsoleCoordinate completionBoxStart)
    {
        var horizontalBorder = TruncateToWindow(new string(BoxDrawing.EdgeHorizontal, wordWidth + theme.SelectedCompletionItemMarker.Length + 1), 2).Text; // +1 = space after item (=space before right border)

        var selectedItem = completionPane.FilteredView.SelectedItem;
        return completionPane.FilteredView
            .Select((completion, index) =>
            {
                var item = completion.DisplayText.Length > 0 ? completion.DisplayText : completion.ReplacementText;
                var isSelected = selectedItem == completion;

                var rowCells = new List<Cell>();

                //left border
                rowCells.AddRange(Cell.FromText(BoxDrawing.EdgeVertical, theme.CompletionBorder));

                //(un)selected item marker
                if (isSelected)
                {
                    rowCells.AddRange(Cell.FromText(theme.SelectedCompletionItemMarker));
                }
                else
                {
                    rowCells.AddRange(Cell.FromText(theme.UnselectedCompletionItemMarker));
                }

                //item
                var itemCells = Cell.FromText(TruncateToWindow(item + new string(' ', wordWidth - item.GetUnicodeWidth()), 2 + theme.SelectedCompletionItemMarker.Length)); // 2 = left border + right border
                if (isSelected)
                {
                    TransformFormattingForSelected(itemCells);
                }
                rowCells.AddRange(itemCells);

                //right border
                rowCells.AddRange(Cell.FromText(" " + BoxDrawing.EdgeVertical, theme.CompletionBorder));

                return new Row(rowCells);
            })
            .Prepend(new Row(Cell.FromText(BoxDrawing.CornerUpperLeft + horizontalBorder + BoxDrawing.CornerUpperRight, theme.CompletionBorder)))
            .Append(new Row(Cell.FromText(BoxDrawing.CornerLowerLeft + horizontalBorder + BoxDrawing.CornerLowerRight, theme.CompletionBorder)))
            .ToArray();

        FormattedString TruncateToWindow(FormattedString line, int offset)
        {
            var availableWidth = Math.Max(0, codeAreaWidth - completionBoxStart.Column - offset);
            return line.Substring(0, Math.Min(line.Length, availableWidth));
        }

        void TransformFormattingForSelected(List<Cell> itemCells)
        {
            for (int i = 0; i < itemCells.Count; i++)
            {
                var cell = itemCells[i];
                if (cell.Formatting.Background is null)
                {
                    var newFormatting = cell.Formatting with { Background = theme.SelectedCompletionItemBackground };
                    itemCells[i] = cell with { Formatting = newFormatting };
                }
            }
        }
    }

    private Row[] BuildDocumentationRows(FormattedString documentation, int maxWidth)
    {
        if (string.IsNullOrEmpty(documentation.Text) || maxWidth < 12)
            return Array.Empty<Row>();

        // request word wrapping. actual line lengths won't be exactly the requested width due to wrapping.
        var requestedBoxWidth = Math.Min(maxWidth, 55);
        var requestedTextWidth = requestedBoxWidth - 3; // 3 because of left padding, right padding, right border
        var wrapped = WordWrapping.WrapWords(documentation.Replace("\r\n", "\n"), requestedTextWidth);
        var actualTextWidth = wrapped.Max(line => line.GetUnicodeWidth());
        var actualBoxWidth = actualTextWidth + 3;

        var (boxTop, boxBottom) = BoxDrawing.HorizontalBorders(actualBoxWidth - 1, leftCorner: false);

        return wrapped
            .Select(line =>
                new Row(Cell
                    .FromText(" " + line.Trim() + new string(' ', actualTextWidth - line.GetUnicodeWidth()))
                    .Concat(Cell.FromText(" " + BoxDrawing.EdgeVertical, theme.DocumentationBorder))
                    .ToList()
                )
            )
            .Prepend(new Row(Cell.FromText(boxTop, theme.DocumentationBorder)))
            .Append(new Row(Cell.FromText(boxBottom, theme.DocumentationBorder)))
            .ToArray();
    }
}
