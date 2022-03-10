#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Diagnostics;
using System.Linq;
using PrettyPrompt.Consoles;

namespace PrettyPrompt.Rendering;

/// <summary>
/// Represents characters (TextElements) rendered on a screen.
/// Used as part of <see cref="IncrementalRendering"/>.
/// </summary>
internal sealed class Screen
{
    private readonly ScreenArea[] screenAreas;

    public int Width { get; }
    public int Height { get; }
    public ConsoleCoordinate Cursor { get; }
    public Cell[] CellBuffer { get; }
    public int MaxIndex { get; }

    public Screen(int width, int height, ConsoleCoordinate cursor, params ScreenArea[] screenAreas)
    {
        this.screenAreas = screenAreas;
        this.Width = width;
        this.Height = screenAreas
            .Select(area => area.TruncateToScreenHeight
                ? Math.Min(height, area.Start.Row + area.Rows.Length)
                : area.Start.Row + area.Rows.Length
            )
            .DefaultIfEmpty()
            .Max();
        this.CellBuffer = new Cell[Width * Height];
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
                var row = area.Start.Row + i;
                var line = area.Rows[i].Cells;
                var position = row * Width + area.Start.Column;
                var length = Math.Min(line.Count, CellBuffer.Length - position);
                if (length > 0)
                {
                    foreach (var cell in line)
                    {
                        cell.TruncateToScreenHeight = area.TruncateToScreenHeight;
                    }
                    line.CopyTo(0, CellBuffer, position, length);
                    maxIndex = Math.Max(maxIndex, position + length);
                }
            }
        }
        return maxIndex;
    }

    /// <summary>
    /// We have our cursor coordinate, but its position represents the position in the input string.
    /// Normally, this is the same as the coordinate on screen, unless we've rendered CJK characters
    /// which are "full width" and take up two characters on screen.
    /// </summary>
    private static ConsoleCoordinate PositionCursor(Screen screen, ConsoleCoordinate cursor)
    {
        if (screen.CellBuffer.Length == 0) return cursor;

        int row = Math.Min(cursor.Row, screen.Height - 1);
        int column = Math.Min(cursor.Column, screen.Width - 1);
        int rowStartIndex = row * screen.Width;
        int rowCursorIndex = rowStartIndex + column;
        int extraColumnOffset = 0;
        for (int i = row * screen.Width; i <= rowCursorIndex + extraColumnOffset; i++)
        {
            var cell = screen.CellBuffer[i];
            if (cell is not null && cell.IsContinuationOfPreviousCharacter)
            {
                Debug.Assert(i > 0);
                var previousCell = screen.CellBuffer[i - 1];
                Debug.Assert(previousCell.ElementWidth == 2);
                Debug.Assert(previousCell.Text is not null);

                //e.g. for '界' is previousCell.ElementWidth==2 and previousCell.Text.Length==1
                //e.g. for '😀' is previousCell.ElementWidth==2 and previousCell.Text.Length==2 (which means cursor is already moved by 2 because of Text length)
                extraColumnOffset += previousCell.ElementWidth - previousCell.Text.Length;
            }
        }
        int newColumn = column + extraColumnOffset;

        return newColumn > screen.Width
            ? new ConsoleCoordinate(row + 1, newColumn - screen.Width)
            : new ConsoleCoordinate(row, newColumn);
    }

    public Screen Resize(int width, int height) => new(width, height, Cursor, screenAreas);
}
