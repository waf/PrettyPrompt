#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;
using System;
using System.Linq;

namespace PrettyPrompt.Rendering
{
    /// <summary>
    /// Represents characters (TextElements) rendered on a screen.
    /// Used as part of <see cref="IncrementalRendering"/>.
    /// </summary>
    sealed class Screen
    {
        private readonly ScreenArea[] screenAreas;

        public int Width { get; }
        public int Height { get;  }
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
            this.Cursor = new ConsoleCoordinate(Math.Min(cursor.Row, Height), Math.Min(cursor.Column, Width));
            this.CellBuffer = new Cell[Width * Height];
            this.MaxIndex = FillCharBuffer(screenAreas);
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
                    // a bit of a hack; some characters earlier in the row may be CJK (double-width). Decrease position to handle.
                    var cjkOffset = CellBuffer[(row*Width)..position].Sum(c => (c?.CellWidth ?? 1) - 1);
                    var length = Math.Min(line.Count, CellBuffer.Length - position);
                    if (length > 0)
                    {
                        foreach (var cell in line)
                        {
                            cell.TruncateToScreenHeight = area.TruncateToScreenHeight;
                        }
                        line.CopyTo(0, CellBuffer, position - cjkOffset, length);
                        maxIndex = Math.Max(maxIndex, position + length);
                    }
                }
            }
            return maxIndex;
        }

        public Screen Resize(int width, int height) =>
            new Screen(width, height, Cursor, screenAreas);
    }
}
