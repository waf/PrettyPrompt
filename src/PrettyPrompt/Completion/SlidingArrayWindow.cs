#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private async Task SetSelectedIndex(int? value, CancellationToken cancellationToken)
    {
        selectedIndex = value;
        var selectedItemChanged = SelectedItemChanged;
        if (selectedItemChanged != null)
        {
            await selectedItemChanged(SelectedItem, cancellationToken).ConfigureAwait(false);
        }
    }

    public SlidingArrayWindow(int windowBuffer = 3)
    {
        this.windowBuffer = windowBuffer;
    }

    public int? SelectedIndexInAllItems => selectedIndex;
    public int? SelectedIndexInVisibleItems => selectedIndex - windowStart;
    public CompletionItem? SelectedItem => !IsEmpty && selectedIndex.HasValue ? itemsSorted[selectedIndex.Value].Item : null;
    public int AllItemsCount => itemsSorted.Count;
    public int VisibleItemsCount => visibleItems.Count;
    public bool IsEmpty => AllItemsCount == 0;
    public IReadOnlyList<CompletionItem> VisibleItems => visibleItems;

    public event Func<CompletionItem?, CancellationToken, Task>? SelectedItemChanged;

    public async Task UpdateItems(IEnumerable<CompletionItem> items, string documentText, int documentCaret, TextSpan spanToReplace, int windowLength, CancellationToken cancellationToken)
    {
        this.windowLength = windowLength;
        this.itemsOriginal.Clear();
        this.itemsOriginal.AddRange(items);

        await Match(documentText, documentCaret, spanToReplace, cancellationToken).ConfigureAwait(false);
        await ResetSelectedIndex(cancellationToken).ConfigureAwait(false);
        UpdateVisibleItems();
    }

    public async Task Match(string documentText, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
    {
        //this could be done more efficiently if we would have in-place stable List<T> sort implementation
        itemsSorted = itemsOriginal
            .Select(i => (Item: i, Priority: i.GetCompletionItemPriority(documentText, caret, spanToBeReplaced)))
            .OrderByDescending(t => t.Priority)
            .Select(t => (t.Item, t.Priority >= 0))
            .ToList();

        UpdateVisibleItems();
        await ResetSelectedIndex(cancellationToken).ConfigureAwait(false);
    }

    public async Task IncrementSelectedIndex(CancellationToken cancellationToken)
    {
        if (!selectedIndex.HasValue)
        {
            await SetSelectedIndex(0, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (selectedIndex == AllItemsCount - 1)
            return;

        await SetSelectedIndex(selectedIndex + 1, cancellationToken).ConfigureAwait(false);

        if (selectedIndex + windowBuffer >= windowStart + windowLength && windowStart + windowLength < AllItemsCount)
        {
            windowStart++;
            UpdateVisibleItems();
        }
    }

    public async Task DecrementSelectedIndex(CancellationToken cancellationToken)
    {
        if (!selectedIndex.HasValue)
        {
            await SetSelectedIndex(0, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (selectedIndex == 0)
            return;

        await SetSelectedIndex(selectedIndex - 1, cancellationToken).ConfigureAwait(false);

        if (selectedIndex - windowBuffer < windowStart && windowStart > 0)
        {
            windowStart--;
            UpdateVisibleItems();
        }
    }

    public async Task Clear(CancellationToken cancellationToken)
    {
        itemsOriginal.Clear();
        itemsSorted.Clear();
        windowLength = 0;
        await ResetSelectedIndex(cancellationToken).ConfigureAwait(false);
        UpdateVisibleItems();
    }

    private async Task ResetSelectedIndex(CancellationToken cancellationToken)
    {
        await SetSelectedIndex(IsEmpty ? null : (itemsSorted[0].IsMatching ? 0 : null), cancellationToken).ConfigureAwait(false);
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
}
