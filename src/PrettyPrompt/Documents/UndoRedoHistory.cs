#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Diagnostics;
using PrettyPrompt.TextSelection;

namespace PrettyPrompt.Documents;

/// <summary>
/// Stores undo/redo history for a single document.
/// </summary>
/// <remarks>
/// Implementation is naive -- it just stores full snapshots in a list and undo/redo navigates
/// through it. If tracking snapshots of the input ends up causing high memory, we can rework it.
/// </remarks>
internal sealed class UndoRedoHistory
{
    private readonly List<Record> history = new();
    private int currentIndex;

    public UndoRedoHistory(string text, int caret)
    {
        history.Add(new Record(text, caret, null));
    }

    internal void Track(ReadOnlyStringBuilder text, int caret, SelectionSpan? selection)
    {
        CheckValidity();

        var historyItem = history[currentIndex];
        if (text.Equals(historyItem.Text))
        {
            if (caret != historyItem.Caret || selection != historyItem.Selection)
            {
                history[currentIndex] = historyItem with { Caret = caret, Selection = selection };
            }
        }
        else
        {
            if (currentIndex != history.Count - 1)
            {
                //edit after undos -> we will throw following redos away
                var itemsToRemove = history.Count - currentIndex - 1;
                history.RemoveRange(index: history.Count - itemsToRemove, itemsToRemove);
            }

            history.Add(new Record(text.ToString(), caret, selection));
            currentIndex = history.Count - 1;
        }
    }

    public Record Undo()
    {
        CheckValidity();

        if (currentIndex > 0)
        {
            --currentIndex;
        }

        return history[currentIndex];
    }

    public Record Redo()
    {
        CheckValidity();

        if (currentIndex < history.Count - 1)
        {
            ++currentIndex;
        }

        return history[currentIndex];
    }

    internal void Clear()
    {
        CheckValidity();

        currentIndex = 0;
        history.RemoveRange(1, history.Count - 1);
    }

    private void CheckValidity()
    {
        Debug.Assert(history.Count > 0);
        Debug.Assert(currentIndex >= 0 && currentIndex < history.Count);
    }

    public readonly record struct Record(string Text, int Caret, SelectionSpan? Selection);
}