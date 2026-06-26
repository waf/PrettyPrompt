#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using PrettyPrompt.Consoles;
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

    public void RenderPrompt(CodePane codePane)
    {
        // write some newlines to ensure we have enough room to render the completion pane.
        var newLinesCount = codePane.EmptySpaceAtBottomOfWindowHeight;
        console.Write(new string('\n', newLinesCount) + GetMoveCursorUp(newLinesCount) + GetMoveCursorToColumn(1) + Reset);
        console.Write(configuration.Prompt);

        // those newlines may have scrolled the buffer up (prompt started near the window bottom); let the code pane
        // correct TopCoordinate, since on non-Windows it can't re-derive it from the OS cursor.
        codePane.AdjustTopCoordinateForReservedLines(newLinesCount);
    }

    public void RenderOutput(
        PromptResult? result,
        CodePane codePane,
        OverloadPane overloadPane,
        CompletionPane completionPane,
        IReadOnlyCollection<FormatSpan> highlights,
        KeyPress key,
        bool forceRedraw = false)
    {
        if (result is not null)
        {
            // reformat-on-submit leaves the on-screen line stale, so repaint it. https://github.com/waf/CSharpRepl/issues/356
            bool redraw = forceRedraw;
            if (wasTextSelectedDuringPreviousRender && codePane.Selection is null)
            {
                redraw = true;
            }

            if (completionPane.IsOpen)
            {
                completionPane.IsOpen = false;
                redraw = true;
            }

            //https://github.com/waf/PrettyPrompt/issues/239
            if (overloadPane.IsOpen)
            {
                overloadPane.IsOpen = false;
                redraw = true;
            }

            if (redraw)
            {
                Redraw();
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
                previouslyRenderedScreen.Dispose(); // return its pooled buffer before we drop it
                previouslyRenderedScreen = new Screen(0, 0, ConsoleCoordinate.Zero);
                console.Clear(); // for some reason, using escape codes (ClearEntireScreen and MoveCursorToPosition) leaves
                                 // CursorTop in an old (cached?) state. Using Console.Clear() works around this.
                RenderPrompt(codePane);
                codePane.MeasureConsole(); // our code pane will have more room to render, it now renders at the top of the screen.
            }

            Redraw();
        }

        wasTextSelectedDuringPreviousRender = codePane.Selection.HasValue;

        void Redraw()
        {
            // convert our "view models" into characters, contained in screen areas
            var codeWidget = BuildCodeScreenArea(codePane, highlights);
            var completionWidgets = BuildCompletionScreenAreas(
                codePane,
                overloadPane,
                completionPane,
                codeAreaWidth: codePane.CodeAreaWidth);

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
        var wrappedLines = codePane.WordWrappedLines;
        int wrappedCount = wrappedLines.Count;

        // If the last wrapped line is full (or ends in a newline), we render an extra blank line below it so
        // the cursor can sit on a fresh line. This depends only on the last wrapped line, so we decide it up
        // front (measuring a single throwaway row) without having to build every row.
        bool trailingBlankLine = LastWrappedLineNeedsTrailingBlank(wrappedLines, codePane.CodeAreaWidth);
        int renderedLineCount = wrappedCount + (trailingBlankLine ? 1 : 0);

        // Compute the visible line range FIRST, then build cells only for those lines. Previously every
        // wrapped line was turned into a Row, and TrimLinesToViewPortSize then sliced out the off-screen
        // ones - which were never placed in a Screen and so never Disposed, leaking their pooled cells on
        // every keystroke for any document taller than the window (see PERFORMANCE_PLAN.md Tier C).
        (int viewPortStart, int viewPortEnd) = GetViewPortRange(codePane.Cursor.Row, renderedLineCount);

        int wrappedEnd = Math.Min(viewPortEnd, wrappedCount);
        var highlightedLines = CellRenderer.ApplyColorToCharacters(
            highlights, wrappedLines, codePane.Selection, configuration.SelectedTextBackground, viewPortStart, wrappedEnd);

        // Append the trailing blank line only if it falls within the viewport (the bottom of the document is visible).
        if (trailingBlankLine && viewPortEnd == renderedLineCount)
        {
            Array.Resize(ref highlightedLines, highlightedLines.Length + 1);
            highlightedLines[^1] = new Row(0);
        }

        var codeWidget = new ScreenArea(ConsoleCoordinate.Zero, highlightedLines, TruncateToScreenHeight: false, ViewPortStart: viewPortStart);
        return codeWidget;
    }

    /// <summary>
    /// Whether an extra blank line should be rendered below the last wrapped line so the cursor can sit on a
    /// fresh line. Measures the real cells of the last wrapped line (cell count and last-cell text are
    /// independent of highlighting/selection) and returns the row to the pool.
    /// </summary>
    private static bool LastWrappedLineNeedsTrailingBlank(IReadOnlyList<Documents.WrappedLine> wrappedLines, int codeAreaWidth)
    {
        using var lastRow = new Row(wrappedLines[^1].Content);
        return lastRow.Length > 0
            && (lastRow.Length >= codeAreaWidth || lastRow[lastRow.Length - 1].Text == "\n");
    }

    /// <summary>
    /// If there are too many lines of code to show in the current console window, return the half-open range
    /// [start, end) of lines that fit in the console window ("viewport"); otherwise the whole document. The
    /// range always contains the cursor line. This replaces the previous build-everything-then-slice approach
    /// (the old TrimLinesToViewPortSize) so callers can build cells for only the visible rows.
    /// </summary>
    private (int viewPortStart, int viewPortEnd) GetViewPortRange(int cursorRow, int renderedLineCount)
    {
        const int BlankBufferLines = 2;
        int height = console.WindowHeight - BlankBufferLines;
        if (renderedLineCount <= height)
        {
            return (0, renderedLineCount);
        }

        int bufferStart = cursorRow - height / 2;
        int bufferEnd = bufferStart + height;
        if (bufferStart < 0)
        {
            bufferStart = 0;
            bufferEnd = bufferStart + height;
        }
        else if (bufferEnd > renderedLineCount)
        {
            bufferStart -= (bufferEnd - renderedLineCount);
            bufferEnd = renderedLineCount;
        }

        return (bufferStart, bufferEnd);
    }

    private ScreenArea[] BuildCompletionScreenAreas(
        CodePane codePane,
        OverloadPane overloadPane,
        CompletionPane completionPane,
        int codeAreaWidth)
    {
        //  _  <-- cursor location
        //  ┌──────────────┬─────────────────────────────┐
        //  │ completion 1 │ documentation box with some |
        //  │ completion 2 │ docs that may wrap.         |
        //  │ completion 3 ├─────────────────────────────┘
        //  └──────────────┘

        var filteredView = completionPane.FilteredView;
        var completionStart = codePane.GetHelperPanesStartPosition();
        ScreenArea overloadArea;
        if (overloadPane.IsOpen)
        {
            overloadArea = BuildOverloadArea(overloadPane, completionStart);
            completionStart = completionStart.Offset(overloadArea.Rows.Length - 1, 0);
        }
        else
        {
            overloadArea = ScreenArea.Empty;
        }

        if (!completionPane.IsOpen || filteredView.IsEmpty)
        {
            if (overloadPane.IsOpen)
            {
                return new[] { overloadArea };
            }
            else
            {
                return Array.Empty<ScreenArea>();
            }
        }

        var completionArea = BuildCompletionArea(completionPane, codeAreaWidth, completionStart);

        var documentationStart = new ConsoleCoordinate(completionStart.Row, completionStart.Column + completionArea.Width - 1);
        var documentationArea = BuildDocumentationArea(completionPane, documentationStart);

        boxDrawing.Connect(overloadArea.Rows, completionArea.Rows, documentationArea.Rows);

        return new[] { overloadArea, completionArea, documentationArea };
    }

    private ScreenArea BuildOverloadArea(OverloadPane overloadPane, ConsoleCoordinate position)
    {
        var rows = boxDrawing.BuildFromLines(
                overloadPane.SelectedItem,
                configuration: configuration,
                background: configuration.CompletionItemDescriptionPaneBackground);
        return new ScreenArea(position, rows);
    }

    private ScreenArea BuildCompletionArea(CompletionPane completionPane, int codeAreaWidth, ConsoleCoordinate position)
    {
        var rows = boxDrawing.BuildFromItemList(
            items: completionPane.FilteredView.VisibleItems.Select(c => c.DisplayTextFormatted),
            configuration: configuration,
            maxWidth: codeAreaWidth - position.Column,
            selectedLineIndex: completionPane.FilteredView.SelectedIndexInVisibleItems);
        return new ScreenArea(position, rows);
    }

    private ScreenArea BuildDocumentationArea(CompletionPane completionPane, ConsoleCoordinate position)
    {
        var documentation = completionPane.SelectedItemDocumentation;
        if (documentation.Count == 0) return ScreenArea.Empty;

        var rows = boxDrawing.BuildFromLines(
             documentation,
             configuration: configuration,
             background: configuration.CompletionItemDescriptionPaneBackground);
        return new ScreenArea(position, rows);
    }

    public void Dispose()
    {
        previouslyRenderedScreen?.Dispose();
    }
}