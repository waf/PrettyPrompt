#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt;

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
    public static readonly Pool SharedPool = new();

    private string? text;
    private bool isContinuationOfPreviousCharacter;
    private int elementWidth;

    public string? Text => text;
    public bool IsContinuationOfPreviousCharacter => isContinuationOfPreviousCharacter;
    public int ElementWidth => elementWidth;

    public ConsoleFormat Formatting;
    public bool TruncateToScreenHeight;

    private bool isPoolable;

    private Cell(bool isPoolable)
    {
        this.isPoolable = isPoolable;
    }

    private void Initialize(string? text, in ConsoleFormat formatting, int elementWidth, bool isContinuationOfPreviousCharacter)
    {
        this.text = text;
        this.Formatting = formatting;

        // full-width handling properties
        this.isContinuationOfPreviousCharacter = isContinuationOfPreviousCharacter;
        this.elementWidth = elementWidth;
    }

    public static void AddTo(List<Cell> cells, FormattedString formattedString)
    {
        // note, this method is fairly hot, please profile when making changes to it.
        // don't use: foreach (var (element, formatting) in formattedString.EnumerateTextElements())
        //            manual enumeration and using by-ref values is faster
        var enumerator = formattedString.EnumerateTextElements();
        while (enumerator.MoveNext())
        {
            ref readonly var elem = ref enumerator.GetCurrentByRef();
            var elementText = StringCache.Shared.Get(elem.Element, out var elementWidth);
            cells.Add(SharedPool.Get(elementText, elem.Formatting, elementWidth));
            for (int i = 1; i < elementWidth; i++)
            {
                cells.Add(SharedPool.Get(null, elem.Formatting, isContinuationOfPreviousCharacter: true));
            }
        }

        Debug.Assert(cells.Count(c => c.text == "\n") <= 1); //otherwise it should be splitted into multiple rows
    }

    public static Cell CreateSingleNonpoolableCell(char character, in ConsoleFormat formatting)
    {
        var list = ListPool<Cell>.Shared.Get(1);
        AddTo(list, new FormattedString(character.ToString(), formatting));
        var cell = list.Single();
        ListPool<Cell>.Shared.Put(list);
        cell.isPoolable = false;
        return cell;
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
            text == other.text &&
            isContinuationOfPreviousCharacter == other.isContinuationOfPreviousCharacter &&
            //ElementWidth == other.ElementWidth && //is given by Text, so we don't need to check
            Formatting.Equals(in other.Formatting) &&
            TruncateToScreenHeight == other.TruncateToScreenHeight;
    }

    private string GetDebuggerDisplay() => text + " " + Formatting.ToString();

    internal class Pool
    {
        private readonly Stack<Cell> pool = new();

        public Cell Get(string? text, in ConsoleFormat formatting, int elementWidth = 1, bool isContinuationOfPreviousCharacter = false)
        {
            Cell? result = null;
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    result = pool.Pop();
                }
            }
            result ??= new Cell(isPoolable: true);
            result.Initialize(text, in formatting, elementWidth, isContinuationOfPreviousCharacter);
            return result;
        }

        public void Put(Cell value)
        {
            if (value.isPoolable)
            {
                lock (pool)
                {
                    pool.Push(value);
                }
            }
        }
    }
}