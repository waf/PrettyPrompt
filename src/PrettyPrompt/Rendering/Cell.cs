#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Diagnostics;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;

namespace PrettyPrompt;

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
/// Represents a single cell in the console, with any associate formatting.
///
/// https://en.wikipedia.org/wiki/Halfwidth_and_fullwidth_forms
/// A character can be full-width (e.g. CJK: Chinese, Japanese, Korean) in
/// which case it will take up two characters on the console, so we represent
/// it as two consecutive cells. The first cell will have <see cref="ElementWidth"/> of 2.
/// the trailing cell will have <see cref="IsContinuationOfPreviousCharacter"/> set to true.
/// </summary>
[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
internal sealed record Cell
{
    public string? Text { get; }
    public bool IsContinuationOfPreviousCharacter { get; }
    public int ElementWidth { get; }

    public ConsoleFormat Formatting { get; set; }
    public bool TruncateToScreenHeight { get; set; }

    private Cell(string? text, ConsoleFormat Formatting, int elementWidth = 1, bool isContinuationOfPreviousCharacter = false)
    {
        this.Text = text;
        this.Formatting = Formatting;

        // full-width handling properties
        this.IsContinuationOfPreviousCharacter = isContinuationOfPreviousCharacter;
        this.ElementWidth = elementWidth;
    }

    public static List<Cell> FromText(char text, ConsoleFormat formatting)
        => FromText(new FormattedString(text.ToString(), formatting));

    public static List<Cell> FromText(string text, ConsoleFormat formatting)
        => FromText(new FormattedString(text, formatting));

    public static List<Cell> FromText(FormattedString formattedString)
    {
        // note, this method is fairly hot, please profile when making changes to it.
        var cells = new List<Cell>(formattedString.Length);
        foreach (var (element, formatting) in formattedString.EnumerateTextElements())
        {
            var elementWidth = UnicodeWidth.GetWidth(element);
            cells.Add(new Cell(element, formatting, elementWidth));
            for (int i = 1; i < elementWidth; i++)
            {
                cells.Add(new Cell(null, formatting, isContinuationOfPreviousCharacter: true));
            }
        }
        return cells;
    }

    public static List<Cell> FromText(string text) => FromText(text, ConsoleFormat.None);

    private string GetDebuggerDisplay() => Text + " " + Formatting.ToString();
}
