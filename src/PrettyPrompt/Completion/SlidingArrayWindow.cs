#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PrettyPrompt.Documents;

namespace PrettyPrompt.Completion;

/// <summary>
/// Datastructure that provides a window over a segment of an array, similar to <see cref="ArraySegment{T}"/>, but
/// also has a concept of the window "sliding" to always keep a selected index in view. This datastructure powers
/// the auto-complete menu, and the window slides to provide the scrolling of the menu.
/// </summary>
internal sealed class SlidingArrayWindow : IReadOnlyCollection<CompletionItem>
{
    private readonly List<CompletionItem> itemsOriginal = new();
    private readonly int windowBuffer;
    private List<(CompletionItem Item, bool IsMatching)> itemsSorted = new();
    private int windowLength;
    private int windowStart;
    private int? selectedIndex;

    public SlidingArrayWindow(int windowBuffer = 3)
    {
        this.windowBuffer = windowBuffer;
    }

    public CompletionItem? SelectedItem => !IsEmpty && selectedIndex.HasValue ? itemsSorted[selectedIndex.Value].Item : null;

    public void UpdateItems(IEnumerable<CompletionItem> items, string documentText, int documentCaret, TextSpan spanToReplace, int windowLength)
    {
        this.windowLength = windowLength;
        this.itemsOriginal.Clear();
        this.itemsOriginal.AddRange(items);

        this.itemsSorted.Clear();
        foreach (var item in items)
        {
            this.itemsSorted.Add((item, false));
        }

        Match(documentText, documentCaret, spanToReplace);

        ResetSelectedIndex();
    }

    public void Match(string documentText, int caret, TextSpan spanToBeReplaced)
    {
        itemsSorted = itemsOriginal
            .Select(i => (Item: i, Priority: i.GetCompletionItemPriority(documentText, caret, spanToBeReplaced)))
            .OrderByDescending(t => t.Priority)
            .Select(t => (t.Item, t.Priority >= 0))
            .ToList();

        ResetSelectedIndex();
    }

    public void IncrementSelectedIndex()
    {
        if (!selectedIndex.HasValue)
        {
            selectedIndex = 0;
            return;
        }

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
        if (!selectedIndex.HasValue)
        {
            selectedIndex = 0;
            return;
        }

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

    private void ResetSelectedIndex()
    {
        selectedIndex = IsEmpty ? null : (itemsSorted[0].IsMatching ? 0 : null);
        windowStart = 0;
    }

    public int Count => itemsSorted.Count;

    public bool IsEmpty => Count == 0;

    int IReadOnlyCollection<CompletionItem>.Count => Count;
    IEnumerator<CompletionItem> IEnumerable<CompletionItem>.GetEnumerator() => VisibleItems.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => VisibleItems.GetEnumerator();

    private IEnumerable<CompletionItem> VisibleItems => itemsSorted.Skip(windowStart).Take(Math.Min(windowLength, Count)).Select(t => t.Item);
}
