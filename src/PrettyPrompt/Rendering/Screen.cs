#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;

namespace PrettyPrompt.Rendering;

/// <summary>
/// Represents characters (TextElements) rendered on a screen.
/// Used as part of <see cref="IncrementalRendering"/>.
/// </summary>
internal sealed class Screen : IDisposable
{
    private readonly ScreenArea[] screenAreas;
    private bool disposed;

    public int Width { get; }
    public int Height { get; }
    public ConsoleCoordinate Cursor { get; }
    public Cell?[] CellBuffer { get; }
    public int MaxIndex { get; }
    public int ViewPortOffset { get; }

    public Screen(int width, int height, ConsoleCoordinate cursor, params ScreenArea[] screenAreas)
    {
        this.screenAreas = screenAreas;
        this.ViewPortOffset = screenAreas.Sum(a => a.ViewPortStart);
        this.Width = width;
        this.Height = screenAreas
            .Select(area => area.TruncateToScreenHeight
                ? Math.Min(height, area.Start.Row + area.Rows.Length)
                : area.Start.Row + area.Rows.Length
            )
            .DefaultIfEmpty()
            .Max();
        this.CellBuffer = ScreenBufferPool.Shared.Get(Width * Height);
        this.MaxIndex = FillCharBuffer(screenAreas);
        this.Cursor = PositionCursor(this, cursor);
    }

    private int FillCharBuffer(ScreenArea[] screenAreas)
    {
        int maxIndex = 0;
        foreach (var area in screenAreas)
        {
            int rowCountToRender = Math.Min(area.Rows.Length, Height - area.Start.Row);
            for (var i = 0; i < rowCountToRender; i++)
            {
                var rowPosition = area.Start.Row + i;
                var row = area.Rows[i];
                var position = rowPosition * Width + area.Start.Column;
                var length = Math.Min(row.Length, CellBuffer.Length - position);
                if (length > 0)
                {
                    for (int cellIndex = 0; cellIndex < row.Length; cellIndex++)
                    {
                        row[cellIndex].TruncateToScreenHeight = area.TruncateToScreenHeight;
                    }
                    row.CopyTo(CellBuffer, position, length);
                    maxIndex = Math.Max(maxIndex, position + length);
                }
            }
        }
        return maxIndex;
    }

    /// <summary>
    /// We have our cursor coordinate, but its position represents the position in the input string.
    /// We need to reposition both the row/column of the cursor based on how they'll be rendered to
    /// the console:
    ///
    /// - For the row: we may only display a subset of the rows based on the current console size,
    ///   so we need to adjust the row position based on the viewport start.
    /// - For the column: repositioning is needed in the case where we've rendered CJK characters.
    ///   These are are "full width" characters and take up two characters on screen.
    ///
    /// </summary>
    private ConsoleCoordinate PositionCursor(Screen screen, ConsoleCoordinate cursor)
    {
        if (screen.CellBuffer.Length == 0) return cursor;

        int row = Math.Min(cursor.Row, screen.Height - 1);
        int charColumn = Math.Min(cursor.Column, screen.Width - 1);
        int rowStartIndex = row * screen.Width;

        // cursor.Column is a char index (UTF-16 offset) within the row. Convert it to a display column by
        // walking the row's cells and summing each cell's ElementWidth (columns it occupies) while counting
        // its Text.Length (chars it consumes). This handles every cluster shape:
        //   '界'  -> 1 char, 2 columns      (wide)
        //   '😀'  -> 2 chars, 2 columns     (surrogate pair)
        //   '🤦🏼‍♂️' -> 7 chars, 2 columns     (ZWJ emoji sequence)
        //   'é' (e + combining accent) -> 2 chars, 1 column (combining cluster)
        int chars = 0;
        int displayColumn = 0;
        for (int i = rowStartIndex; i < rowStartIndex + screen.Width && chars < charColumn; i++)
        {
            var cell = screen.CellBuffer[i];
            if (cell is null)
            {
                // an empty cell is rendered as a single space: one char, one column
                chars++;
                displayColumn++;
            }
            else if (!cell.IsContinuationOfPreviousCharacter)
            {
                // continuation cells carry no text/width of their own - they're accounted for by the
                // ElementWidth of their main cell, so we only advance on main cells.
                chars += cell.Text?.Length ?? 1;
                displayColumn += cell.ElementWidth;
            }
        }

        int newColumn = displayColumn;
        int newRow = cursor.Row - ViewPortOffset;

        return newColumn > screen.Width
            ? new ConsoleCoordinate(newRow + 1, newColumn - screen.Width)
            : new ConsoleCoordinate(newRow, newColumn);
    }

    public Screen Resize(int width, int height) => new(width, height, Cursor, screenAreas);

    public void Dispose()
    {
        // Guard against double-dispose: returning the same buffer to the pool twice would let two screens
        // rent and write the same buffer. (Row.Dispose is already self-guarded, so disposing the areas twice
        // was previously harmless, but the buffer Put is not.)
        if (disposed)
        {
            return;
        }
        disposed = true;

        foreach (var screenArea in screenAreas)
        {
            screenArea.Dispose();
        }
        ScreenBufferPool.Shared.Put(CellBuffer);
    }
}
