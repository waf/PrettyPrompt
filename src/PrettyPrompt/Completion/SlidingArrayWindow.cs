#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PrettyPrompt.Documents;

namespace PrettyPrompt.Completion;

/// <summary>
/// Datastructure that provides a window over a segment of an array, similar to <see cref="ArraySegment{T}"/>, but
/// also has a concept of the window "sliding" to always keep a selected index in view. This datastructure powers
/// the auto-complete menu, and the window slides to provide the scrolling of the menu.
/// </summary>
sealed class SlidingArrayWindow : IReadOnlyCollection<CompletionItem>
{
    private readonly List<CompletionItem> itemsOriginal = new();
    private readonly int windowBuffer;
    private int windowLength;
    private int windowStart;
    private int selectedIndex;
    private List<CompletionItem> itemsSorted=new();

    public SlidingArrayWindow(int windowBuffer = 3)
    {
        this.windowBuffer = windowBuffer;
    }

    /// <summary>
    /// Is not null when IsEmpty==false.
    /// </summary>
    public CompletionItem? SelectedItem => IsEmpty ? default : itemsSorted[selectedIndex];

    public void UpdateItems(IEnumerable<CompletionItem> items, int windowLength)
    {
        this.windowLength = windowLength;
        this.itemsOriginal.Clear();
        this.itemsOriginal.AddRange(items);

        this.itemsSorted.Clear();
        this.itemsSorted.AddRange(this.itemsOriginal);

        this.windowStart = CalculateWindowStart(windowLength, selectedIndex);
    }

    public void Match(string documentText, int caret, TextSpan spanToBeReplaced)
    {
        itemsSorted = itemsOriginal
            .OrderByDescending(i => i.GetCompletionItemPriority(documentText, caret, spanToBeReplaced))
            .ToList();
    }

    public void IncrementSelectedIndex()
    {
        if (selectedIndex == Count - 1)
            return;

        selectedIndex++;

        if (selectedIndex + windowBuffer >= windowStart + windowLength && windowStart + windowLength < Count)
        {
            windowStart++;
        }
    }

    public void DecrementSelectedIndex()
    {
        if (selectedIndex == 0)
            return;

        selectedIndex--;

        if (selectedIndex - windowBuffer < windowStart && windowStart > 0)
        {
            windowStart--;
        }
    }

    public void Clear()
    {
        itemsOriginal.Clear();
        itemsSorted.Clear();
        windowLength = 0;
        ResetSelectedIndex();
    }

    public void ResetSelectedIndex()
    {
        selectedIndex = 0;
        windowStart = 0;
    }

    private int CalculateWindowStart(int windowLength, int selectedIndex) =>
        Count - windowLength <= 0
        ? 0
        : Math.Min(selectedIndex, Count - windowLength);

    public int Count => itemsSorted.Count;

    [MemberNotNullWhen(false, nameof(SelectedItem))]
    public bool IsEmpty => Count == 0;

    int IReadOnlyCollection<CompletionItem>.Count => Count;
    IEnumerator<CompletionItem> IEnumerable<CompletionItem>.GetEnumerator() => VisibleItems.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => VisibleItems.GetEnumerator();

    private IEnumerable<CompletionItem> VisibleItems => itemsSorted.Skip(windowStart).Take(Math.Min(windowLength, Count));
}
