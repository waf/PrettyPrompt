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
    }

    public void RenderOutput(
        PromptResult? result,
        CodePane codePane,
        OverloadPane overloadPane,
        CompletionPane completionPane,
        IReadOnlyCollection<FormatSpan> highlights,
        KeyPress key)
    {
        if (result is not null)
        {
            bool redraw = false;
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