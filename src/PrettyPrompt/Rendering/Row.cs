#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt;

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
            foreach (var cell in cells) Cell.SharedPool.Put(cell);
            ListPool<Cell>.Shared.Put(cells);
            disposed = true;
        }
    }

    public void Add(string text)
        => Add(new FormattedString(text));

    public void Add(string text, in ConsoleFormat formatting)
        => Add(new FormattedString(text, formatting));

    public void Add(FormattedString formattedString)
        => Cell.AddTo(cells, formattedString);

    public void Add(Cell cell)
        => cells.Add(cell);

    public void Replace(int index, Cell cell)
    {
        Cell.SharedPool.Put(cells[index]);
        cells[index] = cell;
    }

    public void CopyTo(Cell?[] cells, int targetPosition, int count)
        => this.cells.CopyTo(0, cells!, targetPosition, count);

    private string GetDebuggerDisplay()
        => string.Join("", cells.Select(c => c.Text));
}