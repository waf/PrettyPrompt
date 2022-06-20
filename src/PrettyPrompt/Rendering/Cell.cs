#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;

namespace PrettyPrompt;

/// <summary>
/// An area of the screen that's being rendered at a coordinate.
/// This is conceptually a UI pane, rasterized into characters.
/// </summary>
internal sealed record ScreenArea(ConsoleCoordinate Start, Row[] Rows, bool TruncateToScreenHeight = true) : IDisposable
{
    public void Dispose()
    {
        foreach (var row in Rows)
        {
            row.Dispose();
        }
#if DEBUG
        Array.Clear(Rows, 0, Rows.Length);
#endif
    }
}

/// <summary>
/// A row of cells. Just here for the readability of method signatures.
/// </summary>
[DebuggerDisplay("Row: {" + nameof(GetDebuggerDisplay) + "()}")]
internal class Row : IDisposable
{
    private readonly List<Cell> cells;
    private bool disposed;

    public int Length => cells.Count;
    public Cell this[int index] => cells[index];

    public Row(int capacity)
    {
        cells = ListPool<Cell>.Shared.Get(capacity);
    }

    public Row(char text, ConsoleFormat formatting)
      : this(new FormattedString(text.ToString(), formatting))
    { }

    public Row(string text)
        : this(new FormattedString(text))
    { }

    public Row(string text, ConsoleFormat formatting)
        : this(new FormattedString(text, formatting))
    { }

    public Row(FormattedString formattedString)
        : this(capacity: 6 * formattedString.Length / 5)
    {
        Cell.AddTo(cells, formattedString);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            ListPool<Cell>.Shared.Put(cells);
            disposed = true;
        }
    }

    public void Add(char text, ConsoleFormat formatting)
        => Add(new FormattedString(text.ToString(), formatting));

    public void Add(string text)
        => Add(text, ConsoleFormat.None);

    public void Add(string text, ConsoleFormat formatting)
        => Add(new FormattedString(text, formatting));

    public void Add(FormattedString formattedString)
        => Cell.AddTo(cells, formattedString);

    public void CopyTo(Cell?[] cells, int targetPosition, int count)
        => this.cells.CopyTo(0, cells!, targetPosition, count);

    private string GetDebuggerDisplay()
        => string.Join("", cells.Select(c => c.Text));
}

/// <summary>
/// Represents a single cell in the console, with any associate formatting.
///
/// https://en.wikipedia.org/wiki/Halfwidth_and_fullwidth_forms
/// A character can be full-width (e.g. CJK: Chinese, Japanese, Korean) in
/// which case it will take up two characters on the console, so we represent
/// it as two consecutive cells. The first cell will have <see cref="ElementWidth"/> of 2.
/// the trailing cell will have <see cref="IsContinuationOfPreviousCharacter"/> set to true.
/// </summary>
//
// Do not change to struct without benchmarking. With some work it's possible, but I tried and performace was much worse.
// This because we are making copies of lists of cells and they are smaller when they are reference types.
// Pooling of cells is currently better.
[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
internal sealed class Cell
{
    public readonly string? Text;
    public readonly bool IsContinuationOfPreviousCharacter;
    public readonly int ElementWidth;

    public ConsoleFormat Formatting;
    public bool TruncateToScreenHeight;

    private Cell(string? text, ConsoleFormat Formatting, int elementWidth = 1, bool isContinuationOfPreviousCharacter = false)
    {
        this.Text = text;
        this.Formatting = Formatting;

        // full-width handling properties
        this.IsContinuationOfPreviousCharacter = isContinuationOfPreviousCharacter;
        this.ElementWidth = elementWidth;
    }

    public static void AddTo(List<Cell> cells, FormattedString formattedString)
    {
        // note, this method is fairly hot, please profile when making changes to it.
        foreach (var (element, formatting) in formattedString.EnumerateTextElements())
        {
            var elementWidth = UnicodeWidth.GetWidth(element);
            cells.Add(new Cell(element, formatting, elementWidth));
            for (int i = 1; i < elementWidth; i++)
            {
                cells.Add(new Cell(null, formatting, isContinuationOfPreviousCharacter: true));
            }
        }

        Debug.Assert(cells.Count(c => c.Text == "\n") <= 1); //otherwise it should be splitted into multiple rows
    }

    public static bool Equals(Cell? left, Cell? right)
    {
        //this is hot from IncrementalRendering.CalculateDiff, so we want to use custom optimized Equals
        if (!ReferenceEquals(left, right))
        {
            if (left is not null)
            {
                return left.Equals(right);
            }
            return false;
        }
        return true;
    }

    public bool Equals(Cell? other)
    {
        //this is hot from IncrementalRendering.CalculateDiff, so we want to use custom optimized Equals
        return
            other is not null &&
            Text == other.Text &&
            IsContinuationOfPreviousCharacter == other.IsContinuationOfPreviousCharacter &&
            //ElementWidth == other.ElementWidth && //is given by Text, so we don't need to check
            Formatting.Equals(in other.Formatting) &&
            TruncateToScreenHeight == other.TruncateToScreenHeight;
    }

    private string GetDebuggerDisplay() => Text + " " + Formatting.ToString();
}