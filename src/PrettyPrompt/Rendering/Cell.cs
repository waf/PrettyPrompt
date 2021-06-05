#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace PrettyPrompt
{
    /// <summary>
    /// An area of the screen that's being rendered at a coordinate.
    /// This is conceptually a UI pane, rasterized into characters.
    /// </summary>
    internal sealed record ScreenArea(ConsoleCoordinate Start, Row[] Rows, bool TruncateToScreenHeight = true)
    {
    }

    /// <summary>
    /// A row of cells. Just here for the readability of method signatures.
    /// </summary>
    internal sealed record Row(List<Cell> Cells);

    /// <summary>
    /// Represents a single character (TextElement) on screen, with any 
    /// associate formatting.
    /// </summary>
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal sealed record Cell
    {
        public string Text { get; }
        public int CellWidth { get; }
        public ConsoleFormat Formatting { get; set;  }
        public bool TruncateToScreenHeight { get; set; }

        private Cell(string text, ConsoleFormat Formatting)
        {
            this.Text = text;
            this.CellWidth = text == "\n" ? 1 : UnicodeWidth.GetWidth(text);
            this.Formatting = Formatting;
        }

        public static List<Cell> FromText(string text, ConsoleFormat formatting)
        {
            // note, this method is fairly hot, please profile when making changes to it.
            var enumerator = StringInfo.GetTextElementEnumerator(text);
            var cells = new List<Cell>(text.Length);

            while(enumerator.MoveNext())
            {
                var element = enumerator.GetTextElement();
                cells.Add(new Cell(element, formatting));
            }
            return cells;
        }

        public static List<Cell> FromText(string text) =>
            FromText(text, null);

        private string GetDebuggerDisplay() =>
            Text + " " + Formatting?.ToString();
    }
}
