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
internal class Renderer : IDisposable
{
    private readonly IConsole console;
    private readonly BoxDrawing boxDrawing;
    private readonly PromptConfiguration configuration;

    private Screen previouslyRenderedScreen = new(0, 0, ConsoleCoordinate.Zero);
    private bool wasTextSelectedDuringPreviousRender;

    public Renderer(IConsole console, PromptConfiguration configuration)
    {
        this.console = console;
        this.boxDrawing = new BoxDrawing(configuration);
        this.configuration = configuration;
    }

    public void RenderPrompt()
    {
        // write some newlines to ensure we have enough room to render the completion pane.
        var min = CompletionPane.VerticalBordersHeight + configuration.MinCompletionItemsCount;
        var max = CompletionPane.VerticalBordersHeight + configuration.MaxCompletionItemsCount;
        var newLinesCount = ((int)(configuration.ProportionOfWindowHeightForCompletionPane * console.WindowHeight)).Clamp(min, max);
        console.Write(new string('\n', newLinesCount) + GetMoveCursorUp(newLinesCount) + GetMoveCursorToColumn(1) + Reset);
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

            console.Write(
                GetMoveCursorDown(codePane.WordWrappedLines.Count - codePane.Cursor.Row - 1)
                + GetMoveCursorToColumn(1)
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
            IncrementalRendering.CalculateDiffAndWriteToConsole(screen, previouslyRenderedScreen, ansiCoordinate, console);
            previouslyRenderedScreen.Dispose();
            previouslyRenderedScreen = screen;
        }
    }

    private static bool DidCodeAreaResize(Screen previousScreen, Screen currentScreen) =>
        previousScreen != null && previousScreen?.Width != currentScreen.Width;

    private ScreenArea BuildCodeScreenArea(CodePane codePane, IReadOnlyCollection<FormatSpan> highlights)
    {
        var highlightedLines = CellRenderer.ApplyColorToCharacters(highlights, codePane.WordWrappedLines, codePane.Selection, configuration.SelectedTextBackground);

        // if we've filled up the full line, add a new line at the end so we can render our cursor on this new line.
        if (highlightedLines[^1].Length > 0
            && (highlightedLines[^1].Length >= codePane.CodeAreaWidth
                || highlightedLines[^1][^1]?.Text == "\n"))
        {
            Array.Resize(ref highlightedLines, highlightedLines.Length + 1);
            highlightedLines[^1] = new Row(0);
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
        //  ┌──────────────┬─────────────────────────────┐
        //  │ completion 1 │ documentation box with some |
        //  │ completion 2 │ docs that may wrap.         |
        //  │ completion 3 ├─────────────────────────────┘
        //  └──────────────┘

        var filteredView = completionPane.FilteredView;
        if (!completionPane.IsOpen || filteredView.IsEmpty)
            return Array.Empty<ScreenArea>();

        int maxCompletionItemWidth = filteredView.VisibleItems.Max(w => UnicodeWidth.GetWidth(w.DisplayText));
        int boxWidth = maxCompletionItemWidth + 3 + configuration.SelectedCompletionItemMarker.Length; // 3 = left border + right border + space before right border

        var completionStart = new ConsoleCoordinate(
            row: cursor.Row + 1,
            column: boxWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                : cursor.Column + boxWidth >= codeAreaWidth ? codeAreaWidth - boxWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                : cursor.Column // enough room, we'll show the completion box offset at the cursor location.
        );
        var completionRows = BuildCompletionRows(completionPane, codeAreaWidth, completionStart);

        var documentationStart = new ConsoleCoordinate(cursor.Row + 1, completionStart.Column + boxWidth - 1);
        var selectedItemDescription = filteredView.SelectedItem != null ? await filteredView.SelectedItem.GetExtendedDescriptionAsync(cancellationToken).ConfigureAwait(false) : default;
        var documentationRows = BuildDocumentationRows(
            documentation: selectedItemDescription,
            maxWidth: codeAreaWidth - completionStart.Column - boxWidth,
            completionRowsCount: completionRows.Length
        );

        boxDrawing.Connect(completionRows, documentationRows);

        var completionArea = new ScreenArea(completionStart, completionRows);
        var documentationArea = new ScreenArea(documentationStart, documentationRows);
        return new[] { completionArea, documentationArea };
    }

    private Row[] BuildCompletionRows(CompletionPane completionPane, int codeAreaWidth, ConsoleCoordinate completionBoxStart)
    {
        return boxDrawing.BuildFromItemList(
            items: completionPane.FilteredView.VisibleItems.Select(c => c.DisplayTextFormatted),
            configuration: configuration,
            maxWidth: codeAreaWidth - completionBoxStart.Column,
            selectedLineIndex: completionPane.FilteredView.SelectedIndexInVisibleItems);
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

        return boxDrawing.BuildFromLines(
            lines: documentationLines,
            configuration: configuration,
            background: configuration.CompletionItemDescriptionPaneBackground);

        List<FormattedString> GetDocumentationLines(int requestedBoxWidth)
        {
            var requestedTextWidth = requestedBoxWidth - CompletionPane.HorizontalBordersWidth;
            var documentationLines = WordWrapping.WrapWords(documentation, requestedTextWidth);
            return documentationLines;
        }

        static int GetActualTextWidth(List<FormattedString> documentationLines)
            => documentationLines.Max(line => line.GetUnicodeWidth());
    }

    public void Dispose()
    {
        previouslyRenderedScreen?.Dispose();
    }
}