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
        public int Width { get; }
        public int Height { get;  }
        public ConsoleCoordinate Cursor { get; }
        public Cell[] CharBuffer { get; }
        public int MaxIndex { get; }

        public Screen(int width, ConsoleCoordinate cursor, params ScreenArea[] screenAreas)
        {
            this.Width = width;
            this.Height = screenAreas.Select(a => a.Start.Row + a.Rows.Length).DefaultIfEmpty().Max();
            this.Cursor = cursor;
            this.CharBuffer = new Cell[Width * Height];

            foreach (var area in screenAreas)
            {
                for(var i = 0; i < area.Rows.Length; i++)
                {
                    var row = area.Start.Row + i;
                    var line = area.Rows[i].Cells;
                    var position = row * Width + area.Start.Column;
                    var length = Math.Min(line.Length, CharBuffer.Length - position);
                    if(length > 0)
                    {
                        Array.Copy(line, 0, CharBuffer, position, length);
                        this.MaxIndex = Math.Max(this.MaxIndex, position + length);
                    }
                }
            }
        }
    }
}
