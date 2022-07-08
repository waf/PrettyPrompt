#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using PrettyPrompt.Documents;

namespace PrettyPrompt.Completion;

/// <summary>
/// Datastructure that provides a window over a segment of an array, similar to <see cref="ArraySegment{T}"/>, but
/// also has a concept of the window "sliding" to always keep a selected index in view. This datastructure powers
/// the auto-complete menu, and the window slides to provide the scrolling of the menu.
/// </summary>
internal sealed class SlidingArrayWindow
{
    private readonly List<CompletionItem> itemsOriginal = new();
    private readonly int windowBuffer;
    private readonly List<CompletionItem> visibleItems = new();
    private List<(CompletionItem Item, bool IsMatching)> itemsSorted = new();
    private int windowLength;
    private int windowStart;
    private int? selectedIndex;

    public SlidingArrayWindow(int windowBuffer = 3)
    {
        this.windowBuffer = windowBuffer;
    }

    public int? SelectedIndexInAllItems => selectedIndex;
    public int? SelectedIndexInVisibleItems => selectedIndex - windowStart;
    public CompletionItem? SelectedItem => !IsEmpty && selectedIndex.HasValue ? itemsSorted[selectedIndex.Value].Item : null;

    public void UpdateItems(IEnumerable<CompletionItem> items, string documentText, int documentCaret, TextSpan spanToReplace, int windowLength)
    {
        this.windowLength = windowLength;
        this.itemsOriginal.Clear();
        this.itemsOriginal.AddRange(items);

        Match(documentText, documentCaret, spanToReplace);
        ResetSelectedIndex();
        UpdateVisibleItems();
    }

    public void Match(string documentText, int caret, TextSpan spanToBeReplaced)
    {
        //this could be done more efficiently if we would have in-place stable List<T> sort implementation
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

        if (selectedIndex == AllItemsCount - 1)
            return;

        selectedIndex++;

        if (selectedIndex + windowBuffer >= windowStart + windowLength && windowStart + windowLength < AllItemsCount)
        {
            windowStart++;
            UpdateVisibleItems();
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
            UpdateVisibleItems();
        }
    }

    public void Clear()
    {
        itemsOriginal.Clear();
        itemsSorted.Clear();
        windowLength = 0;
        ResetSelectedIndex();
        UpdateVisibleItems();
    }

    private void ResetSelectedIndex()
    {
        selectedIndex = IsEmpty ? null : (itemsSorted[0].IsMatching ? 0 : null);
        windowStart = 0;
    }

    private void UpdateVisibleItems()
    {
        visibleItems.Clear();
        var count = Math.Min(windowLength, AllItemsCount);
        for (int i = windowStart; i < windowStart + count; i++)
        {
            visibleItems.Add(itemsSorted[i].Item);
        }
    }

    public int AllItemsCount => itemsSorted.Count;
    public int VisibleItemsCount => visibleItems.Count;

    public bool IsEmpty => AllItemsCount == 0;

    public IReadOnlyList<CompletionItem> VisibleItems => visibleItems;
}
