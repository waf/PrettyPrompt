#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.ConsoleKey;

namespace PrettyPrompt.History;

sealed class HistoryLog : IKeyPressHandler
{
    private const int MaxHistoryEntries = 500;
    private const int HistoryTrimInterval = 100;

    /// <summary>
    /// The actual history, stored as a linked list so we can efficiently go next/prev
    /// </summary>
    private readonly LinkedList<Document> history = new();

    /// <summary>
    /// The currently active history item. Usually, it's the last element of <see cref="history"/>, unless
    /// the user is navigating next/prev in history.
    /// </summary>
    private LinkedListNode<Document> current;

    /// <summary>
    /// The currently code pane being edited. The contents of this pane will be changed when
    /// navigating through the history.
    /// </summary>
    private CodePane latestCodePane;

    /// <summary>
    /// In the case where the user leaves some unsubmitted text on their prompt (the latestCodePane), we capture
    /// it so we can restore it when the user stops navigating through history (i.e. they press Down Arrow until
    /// they're back to their current prompt).
    /// </summary>
    private Document unsubmittedBuffer;

    /// <summary>
    /// Filepath of the history storage file. If null, history is not saved. History is stored as base64 encoded lines,
    /// so we can efficiently append to the file, and not have to worry about newlines in the history entries.
    /// </summary>
    private readonly string persistentHistoryFilepath;
    private readonly Task loadPersistentHistoryTask;

    public HistoryLog(string persistentHistoryFilepath)
    {
        this.persistentHistoryFilepath = persistentHistoryFilepath;

        this.loadPersistentHistoryTask = !string.IsNullOrEmpty(persistentHistoryFilepath)
            ? LoadTrimmedHistoryAsync(persistentHistoryFilepath)
            : Task.CompletedTask;
    }

    private async Task LoadTrimmedHistoryAsync(string persistentHistoryFilepath)
    {
        if (!File.Exists(persistentHistoryFilepath)) return;

        var allHistoryLines = await File.ReadAllLinesAsync(persistentHistoryFilepath).ConfigureAwait(false);
        var loadedHistoryLines = allHistoryLines.TakeLast(MaxHistoryEntries).ToArray();

        // populate history
        for (int i = loadedHistoryLines.Length - 1; i >= 0; i--)
        {
            var entry = Encoding.UTF8.GetString(Convert.FromBase64String(loadedHistoryLines[i]));
            history.AddFirst(new Document(entry, 0));
        }

        // trim history.
        // when we have a lot of history, we don't want to constantly trim the history every launch.
        // instead, use the trim interval to only periodically trim the history.
        if (allHistoryLines.Length > MaxHistoryEntries + HistoryTrimInterval)
        {
            await File.WriteAllLinesAsync(persistentHistoryFilepath, loadedHistoryLines).ConfigureAwait(false);
        }
    }

    public Task OnKeyDown(KeyPress key) => Task.CompletedTask;

    public async Task OnKeyUp(KeyPress key)
    {
        await loadPersistentHistoryTask.ConfigureAwait(false);

        if (history.Count == 0 || key.Handled) return;

        switch (key.Pattern)
        {
            case UpArrow when current.Previous is not null:
                if (current == history.Last)
                {
                    unsubmittedBuffer = history.Last.Value?.Clone() ?? new Document();
                }
                var matchingPreviousEntry = FindPreviousMatchingEntry(unsubmittedBuffer, current);
                SetContents(latestCodePane, matchingPreviousEntry.Value);
                current = matchingPreviousEntry;
                key.Handled = true;
                break;
            case DownArrow when current.Next is not null:
                SetContents(
                    latestCodePane,
                    current.Next == history.Last && unsubmittedBuffer is not null
                        ? unsubmittedBuffer
                        : current.Next.Value
                );
                current = current.Next;
                key.Handled = true;
                break;
            case UpArrow:
            case DownArrow:
                break;
            default:
                unsubmittedBuffer = null;
                current = history.Last;
                key.Handled = false;
                break;
        }

        return;
    }

    /// <summary>
    /// Starting at the <paramref name="currentEntry"/> node, search backwards for a node
    /// that starts with <paramref name="prefix"/>
    /// </summary>
    private static LinkedListNode<Document> FindPreviousMatchingEntry(Document prefix, LinkedListNode<Document> currentEntry)
    {
        if (prefix.Length == 0) return currentEntry.Previous;

        string prefixText = prefix.GetText();

        for (var node = currentEntry.Previous; node is not null; node = node.Previous)
        {
            if (node.Value.StartsWith(prefixText))
            {
                return node;
            }
        }
        return currentEntry;
    }

    private static void SetContents(CodePane codepane, Document contents)
    {
        if (codepane.Document.Equals(contents)) return;

        codepane.Document.Clear();
        codepane.Document.InsertAtCaret(contents.GetText());
        codepane.WordWrap();
    }

    internal void Track(CodePane codePane)
    {
        PruneHistory(history);
        current = history.AddLast(codePane.Document);
        latestCodePane = codePane;
    }

    /// <summary>
    /// Remove the latest history entry, if it's empty or duplicate.
    /// </summary>
    private static void PruneHistory(LinkedList<Document> history)
    {
        if (!history.Any())
        {
            return;
        }

        var previousEntry = history.Last?.Value.GetText();
        var penultimateEntry = history.Last?.Previous?.Value.GetText();
        if (string.IsNullOrEmpty(previousEntry) || previousEntry == penultimateEntry)
        {
            // Remove last empty/duplicate history.
            history.RemoveLast();
        }

        // discard undo/redo history to reduce memory usage
        if (history.Last is not null)
        {
            history.Last.Value.ClearUndoRedoHistory();
        }
    }

    internal async Task SavePersistentHistoryAsync(string input)
    {
        if (input.Length == 0 || string.IsNullOrEmpty(persistentHistoryFilepath)) return;

        var entry = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        await File.AppendAllLinesAsync(persistentHistoryFilepath, new[] { entry }).ConfigureAwait(false);
    }
}
