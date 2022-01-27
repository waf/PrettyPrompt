#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PrettyPrompt.Documents;

/// <summary>
/// Stores undo/redo history for a single document.
/// </summary>
/// <remarks>
/// Implementation is naive -- it just stores full snapshots in a linked and undo/redo navigates
/// through it. If tracking snapshots of the input ends up causing high memory, we can rework it.
/// </remarks>
internal sealed class UndoRedoHistory
{
    private readonly List<StringBuilderWithCaret> history = new();
    private int currentIndex;

    public UndoRedoHistory(StringBuilderWithCaret text)
    {
        history.Add(text.Clone());
    }

    internal void Track(StringBuilderWithCaret text)
    {
        CheckValidity();

        if (!history[currentIndex].EqualsText(text))
        {
            if (currentIndex != history.Count - 1)

            {
                //edit after undos -> we will throw following redos away
                var itemsToRemove = history.Count - currentIndex - 1;
                history.RemoveRange(1, itemsToRemove);
            }

            history.Add(text.Clone());
            currentIndex = history.Count - 1;
        }
    }

    public StringBuilderWithCaret Undo()
    {
        CheckValidity();

        if (currentIndex > 0)
        {
            --currentIndex;
        }

        return history[currentIndex].Clone();
    }

    public StringBuilderWithCaret Redo()
    {
        CheckValidity();

        if (currentIndex < history.Count - 1)
        {
            ++currentIndex;
        }

        return history[currentIndex].Clone();
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
}