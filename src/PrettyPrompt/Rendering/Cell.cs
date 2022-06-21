#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;

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
    public static readonly Pool<Cell, InitArg> SharedPool = new(() => new(), (Cell c, in InitArg arg) => c.Initialize(arg));

    private string? text;
    private bool isContinuationOfPreviousCharacter;
    private int elementWidth;

    public string? Text => text;
    public bool IsContinuationOfPreviousCharacter => isContinuationOfPreviousCharacter;
    public int ElementWidth => elementWidth;

    public ConsoleFormat Formatting;
    public bool TruncateToScreenHeight;

    private Cell() { }

    private void Initialize(InitArg arg)
    {
        this.text = arg.Text;
        this.Formatting = arg.Formatting;

        // full-width handling properties
        this.isContinuationOfPreviousCharacter = arg.IsContinuationOfPreviousCharacter;
        this.elementWidth = arg.ElementWidth;
    }

    public static void AddTo(List<Cell> cells, FormattedString formattedString)
    {
        // note, this method is fairly hot, please profile when making changes to it.
        foreach (var (element, formatting) in formattedString.EnumerateTextElements())
        {
            var elementText = StringCache.Shared.Get(element, out var elementWidth);
            cells.Add(SharedPool.Get(new InitArg(elementText, formatting, elementWidth)));
            for (int i = 1; i < elementWidth; i++)
            {
                cells.Add(SharedPool.Get(new InitArg(null, formatting, isContinuationOfPreviousCharacter: true)));
            }
        }

        Debug.Assert(cells.Count(c => c.text == "\n") <= 1); //otherwise it should be splitted into multiple rows
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

    public readonly struct InitArg
    {
        public readonly string? Text;
        public readonly ConsoleFormat Formatting;
        public readonly int ElementWidth;
        public readonly bool IsContinuationOfPreviousCharacter;

        public InitArg(string? text, ConsoleFormat formatting, int elementWidth = 1, bool isContinuationOfPreviousCharacter = false)
        {
            Text = text;
            Formatting = formatting;
            ElementWidth = elementWidth;
            IsContinuationOfPreviousCharacter = isContinuationOfPreviousCharacter;
        }
    }
}