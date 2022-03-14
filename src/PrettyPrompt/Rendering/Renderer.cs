#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
    private readonly PromptConfiguration configuration;

    private Screen previouslyRenderedScreen = new(0, 0, ConsoleCoordinate.Zero);
    private bool wasTextSelectedDuringPreviousRender;

    public Renderer(IConsole console, PromptConfiguration configuration)
    {
        this.console = console;
        this.configuration = configuration;
    }

    public void RenderPrompt()
    {
        // write some newlines to ensure we have enough room to render the completion pane.
        var min = CompletionPane.VerticalBordersHeight + configuration.MinCompletionItemsCount;
        var max = CompletionPane.VerticalBordersHeight + configuration.MaxCompletionItemsCount;
        var newLinesCount = ((int)(configuration.ProportionOfWindowHeightForCompletionPane * console.WindowHeight)).Clamp(min, max);
        console.Write(new string('\n', newLinesCount) + MoveCursorUp(newLinesCount) + MoveCursorToColumn(1) + Reset);
        console.Write(configuration.Prompt);
    }

    public async Task RenderOutput(
        PromptResult? result,
        CodePane codePane,
        CompletionPane completionPane,
        IReadOnlyCollection<FormatSpan> highlights,
        KeyPress key,
        CancellationToken cancellationToken)
    {
        if (result is not null)
        {
            if (wasTextSelectedDuringPreviousRender && codePane.Selection is null)
            {
                await Redraw(cancellationToken).ConfigureAwait(false);
            }

            if (completionPane.IsOpen)
            {
                completionPane.IsOpen = false;
                await Redraw(cancellationToken).ConfigureAwait(false);
            }

            Write(
                MoveCursorDown(codePane.WordWrappedLines.Count - codePane.Cursor.Row - 1)
                + MoveCursorToColumn(1)
                + "\n"
                + ClearToEndOfScreen,
                hideCursor: true
            );
        }
        else
        {
            if (key.ObjectPattern is (Control, L))
            {
                previouslyRenderedScreen = new Screen(0, 0, ConsoleCoordinate.Zero);
                console.Clear(); // for some reason, using escape codes (ClearEntireScreen and MoveCursorToPosition) leaves
                                 // CursorTop in an old (cached?) state. Using Console.Clear() works around this.
                RenderPrompt();
                codePane.MeasureConsole(); // our code pane will have more room to render, it now renders at the top of the screen.
            }

            await Redraw(cancellationToken).ConfigureAwait(false);
        }

        wasTextSelectedDuringPreviousRender = codePane.Selection.HasValue;

        async Task Redraw(CancellationToken cancellationToken)
        {
            // convert our "view models" into characters, contained in screen areas
            ScreenArea codeWidget = BuildCodeScreenArea(codePane, highlights);
            ScreenArea[] completionWidgets = await BuildCompletionScreenAreas(
                completionPane,
                cursor: codePane.Cursor,
                codeAreaStartColumn: configuration.Prompt.Length,
                codeAreaWidth: codePane.CodeAreaWidth,
                cancellationToken
            ).ConfigureAwait(false);

            // ansi escape sequence row/column values are 1-indexed.
            var ansiCoordinate = new ConsoleCoordinate
            (
                row: 1 + codePane.TopCoordinate,
                column: 1 + configuration.Prompt.Length
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
        var highlightedLines = CellRenderer.ApplyColorToCharacters(highlights, codePane.WordWrappedLines, codePane.Selection);
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

    private async Task<ScreenArea[]> BuildCompletionScreenAreas(
        CompletionPane completionPane,
        ConsoleCoordinate cursor,
        int codeAreaStartColumn,
        int codeAreaWidth,
        CancellationToken cancellationToken)
    {
        //  _  <-- cursor location
        //  ┌──────────────┐
        //  │ completion 1 │ documentation box with some
        //  │ completion 2 │ docs that may wrap.
        //  │ completion 3 │ 
        //  └──────────────┘

        var filteredView = completionPane.FilteredView;
        if (!completionPane.IsOpen || filteredView.IsEmpty)
            return Array.Empty<ScreenArea>();

        int wordWidth = filteredView.Max(w => UnicodeWidth.GetWidth(w.DisplayText));
        int boxWidth = wordWidth + 3 + configuration.SelectedCompletionItemMarker.Length; // 3 = left border + right border + space before right border

        var completionStart = new ConsoleCoordinate(
            row: cursor.Row + 1,
            column: boxWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                : cursor.Column + boxWidth >= codeAreaWidth ? codeAreaWidth - boxWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                : cursor.Column // enough room, we'll show the completion box offset at the cursor location.
        );
        var completionRows = BuildCompletionRows(completionPane, codeAreaWidth, wordWidth, completionStart);

        var documentationStart = new ConsoleCoordinate(cursor.Row + 1, completionStart.Column + boxWidth);
        var selectedItemDescription = filteredView.SelectedItem != null ? await filteredView.SelectedItem.GetExtendedDescriptionAsync(cancellationToken).ConfigureAwait(false) : default;
        var documentationRows = BuildDocumentationRows(
            documentation: selectedItemDescription,
            maxWidth: codeAreaWidth - completionStart.Column - boxWidth,
            completionRowsCount: completionRows.Length
        );

        var completionArea = new ScreenArea(completionStart, completionRows);
        var documentationArea = new ScreenArea(documentationStart, documentationRows);
        var connectionHeight = Math.Max(0, documentationRows.Length - completionRows.Length);
        var completionTopRightCorner = new ConsoleCoordinate(completionStart.Row, completionStart.Column + boxWidth - 1);
        if (connectionHeight > 0)
        {
            var connectionRow = new Row(Cell.FromText(BoxDrawing.EdgeVertical.ToString(), configuration.CompletionBoxBorderFormat));
            var connectionRows = Enumerable.Repeat(connectionRow, connectionHeight - 1)
                .Prepend(new Row(Cell.FromText(BoxDrawing.EdgeVerticalAndLeftHorizontal.ToString(), configuration.CompletionBoxBorderFormat)))
                .Append(new Row(Cell.FromText(BoxDrawing.CornerLowerLeft.ToString(), configuration.CompletionBoxBorderFormat)))
                .ToArray();

            var completionBottomRightCorner = new ConsoleCoordinate(completionStart.Row + completionRows.Length - 1, completionStart.Column + boxWidth - 1);
            var connectionArea = new ScreenArea(completionBottomRightCorner, connectionRows);

            var topRightCornerRow = new Row(Cell.FromText(BoxDrawing.EdgeHorizontalAndLowerVertical.ToString(), configuration.CompletionBoxBorderFormat));
            var topRightCornerArea = new ScreenArea(completionTopRightCorner, new[] { topRightCornerRow });

            return new[] { completionArea, documentationArea, topRightCornerArea, connectionArea };
        }
        else
        {
            if (documentationRows.Length > 0)
            {
                var topRightCornerRow = new Row(Cell.FromText(BoxDrawing.EdgeHorizontalAndLowerVertical.ToString(), configuration.CompletionBoxBorderFormat));
                var topRightCornerArea = new ScreenArea(completionTopRightCorner, new[] { topRightCornerRow });

                var lowerConnectionCorner = new ConsoleCoordinate(completionStart.Row + documentationRows.Length - 1, completionStart.Column + boxWidth - 1);
                var bottomRightCornerRow = new Row(Cell.FromText(documentationRows.Length < completionRows.Length ? BoxDrawing.EdgeVerticalAndRightHorizontal.ToString() : BoxDrawing.EdgeHorizontalAndUpperVertical.ToString(), configuration.CompletionBoxBorderFormat));
                var bottomRightCornerArea = new ScreenArea(lowerConnectionCorner, new[] { bottomRightCornerRow });

                return new[] { completionArea, documentationArea, topRightCornerArea, bottomRightCornerArea };
            }
            else
            {
                return new[] { completionArea, documentationArea, };
            }
        }
    }

    private Row[] BuildCompletionRows(CompletionPane completionPane, int codeAreaWidth, int wordWidth, ConsoleCoordinate completionBoxStart)
    {
        var horizontalBorder = TruncateToWindow(new string(BoxDrawing.EdgeHorizontal, wordWidth + configuration.SelectedCompletionItemMarker.Length + 1), 2).Text; // +1 = space after item (=space before right border)

        var selectedItem = completionPane.FilteredView.SelectedItem;
        return completionPane.FilteredView
            .Select((completion, index) =>
            {
                var item = completion.DisplayTextFormatted;
                var isSelected = selectedItem == completion;

                var rowCells = new List<Cell>();

                //left border
                rowCells.AddRange(Cell.FromText(BoxDrawing.EdgeVertical, configuration.CompletionBoxBorderFormat));

                //(un)selected item marker
                if (isSelected)
                {
                    rowCells.AddRange(Cell.FromText(configuration.SelectedCompletionItemMarker));
                }
                else
                {
                    rowCells.AddRange(Cell.FromText(configuration.UnselectedCompletionItemMarker));
                }

                //item
                var itemCells = Cell.FromText(TruncateToWindow(item + new string(' ', wordWidth - item.GetUnicodeWidth()), 2 + configuration.SelectedCompletionItemMarker.Length)); // 2 = left border + right border
                if (isSelected)
                {
                    TransformBackground(itemCells, configuration.SelectedCompletionItemBackground);
                }
                rowCells.AddRange(itemCells);

                //right border
                rowCells.AddRange(Cell.FromText(" " + BoxDrawing.EdgeVertical, configuration.CompletionBoxBorderFormat));

                return new Row(rowCells);
            })
            .Prepend(new Row(Cell.FromText(BoxDrawing.CornerUpperLeft + horizontalBorder + BoxDrawing.CornerUpperRight, configuration.CompletionBoxBorderFormat)))
            .Append(new Row(Cell.FromText(BoxDrawing.CornerLowerLeft + horizontalBorder + BoxDrawing.CornerLowerRight, configuration.CompletionBoxBorderFormat)))
            .ToArray();

        FormattedString TruncateToWindow(FormattedString line, int offset)
        {
            var availableWidth = Math.Max(0, codeAreaWidth - completionBoxStart.Column - offset);
            return line.Substring(0, Math.Min(line.Length, availableWidth));
        }
    }

    private Row[] BuildDocumentationRows(FormattedString documentation, int maxWidth, int completionRowsCount)
    {
        if (string.IsNullOrEmpty(documentation.Text) || maxWidth < 12)
            return Array.Empty<Row>();

        documentation = documentation.Replace("\r\n", "\n");

        // Request word wrapping. Actual line lengths won't be exactly the requested width due to wrapping.
        // We will try wrappings with different available horizontal sizes. We don't want
        // 'too long and too thin' boxes but also we don't want 'too narrow and too high' ones.
        // So we use two heuristics to select the 'right' proportions of the documentation box.
        List<FormattedString>? documentationLines = null;
        for (double proportion = 0.7; proportion <= 0.96; proportion += 0.05) //70%, 75%, ..., 95%
        {
            var requestedBoxWidth = (int)(proportion * maxWidth);
            documentationLines = GetDocumentationLines(requestedBoxWidth);

            var documentationBoxHeight = documentationLines.Count + CompletionPane.VerticalBordersHeight;

            //Heuristic 1) primarily we want to use space preallocated by the completion items box.
            if (documentationBoxHeight <= completionRowsCount)
            {
                var documentationBoxWidth = GetActualTextWidth(documentationLines) + CompletionPane.HorizontalBordersWidth;

                //Heuristic 2) we prefer boxes with an aspect ratio > 4 (which assumes we are trying different proportions in ascending order).
                const double MonospaceFontWidthHeightRatioApprox = 0.5;
                if (MonospaceFontWidthHeightRatioApprox * documentationBoxWidth / documentationBoxHeight > 4)
                {
                    break;
                }
            }
        }

        Debug.Assert(documentationLines != null);
        var actualTextWidth = GetActualTextWidth(documentationLines);
        var actualBoxWidth = actualTextWidth + CompletionPane.HorizontalBordersWidth;

        var (boxTop, boxBottom) = BoxDrawing.HorizontalBorders(actualBoxWidth - 1, leftCorner: false);

        return documentationLines
            .Select(line =>
            {
                var cells = Cell.FromText(" " + line.Trim() + new string(' ', actualTextWidth - line.GetUnicodeWidth() + 1));
                TransformBackground(cells, configuration.CompletionItemDescriptionPaneBackground);
                cells.AddRange(Cell.FromText(BoxDrawing.EdgeVertical, configuration.CompletionBoxBorderFormat));
                return new Row(cells);
            }
            )
            .Prepend(new Row(Cell.FromText(boxTop, configuration.CompletionBoxBorderFormat)))
            .Append(new Row(Cell.FromText(boxBottom, configuration.CompletionBoxBorderFormat)))
            .ToArray();

        List<FormattedString> GetDocumentationLines(int requestedBoxWidth)
        {
            var requestedTextWidth = requestedBoxWidth - CompletionPane.HorizontalBordersWidth;
            var documentationLines = WordWrapping.WrapWords(documentation, requestedTextWidth);
            return documentationLines;
        }

        static int GetActualTextWidth(List<FormattedString> documentationLines)
            => documentationLines.Max(line => line.GetUnicodeWidth());
    }

    private static void TransformBackground(List<Cell> itemCells, AnsiColor? background)
    {
        for (int i = 0; i < itemCells.Count; i++)
        {
            var cell = itemCells[i];
            if (cell.Formatting.Background is null)
            {
                var newFormatting = cell.Formatting with { Background = background };
                itemCells[i] = cell with { Formatting = newFormatting };
            }
        }
    }
}