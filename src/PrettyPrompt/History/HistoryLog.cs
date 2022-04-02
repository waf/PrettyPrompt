#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Panes;
using static System.ConsoleKey;

namespace PrettyPrompt.History;

internal sealed class HistoryLog : IKeyPressHandler
{
    private const int MaxHistoryEntries = 500;
    private const int HistoryTrimInterval = 100;

    private readonly List<string> history = new();

    /// <summary>
    /// In the case where the user leaves some unsubmitted text on their prompt (the latestCodePane), we capture
    /// it so we can restore it when the user stops navigating through history (i.e. they press Down Arrow until
    /// they're back to their current prompt).
    /// </summary>
    private readonly StringBuilder unsubmittedBuffer = new();

    /// <summary>
    /// Filepath of the history storage file. If null, history is not saved. History is stored as base64 encoded lines,
    /// so we can efficiently append to the file, and not have to worry about newlines in the history entries.
    /// </summary>
    private readonly string? persistentHistoryFilepath;
    private readonly Task loadPersistentHistoryTask;

    private int currentIndex = -1;

    /// <summary>
    /// The currently code pane being edited. The contents of this pane will be changed when
    /// navigating through the history.
    /// </summary>
    private CodePane? codePane;

    public HistoryLog(string? persistentHistoryFilepath)
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
        foreach (var line in loadedHistoryLines)
        {
            var entry = Encoding.UTF8.GetString(Convert.FromBase64String(line));
            history.Add(entry);
        }

        // trim history.
        // when we have a lot of history, we don't want to constantly trim the history every launch.
        // instead, use the trim interval to only periodically trim the history.
        if (allHistoryLines.Length > MaxHistoryEntries + HistoryTrimInterval)
        {
            await File.WriteAllLinesAsync(persistentHistoryFilepath, loadedHistoryLines).ConfigureAwait(false);
        }
    }

    public Task OnKeyDown(KeyPress key, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnKeyUp(KeyPress key, CancellationToken cancellationToken)
    {
        if (codePane is null) return;

        await loadPersistentHistoryTask.ConfigureAwait(false);

        if (history.Count == 0 || key.Handled) return;

        var contents = codePane.Document.GetText();
        if (contents.Contains('\n'))
        {
            //we do not want to cycle in history in multiline documents
            return;
        }
        
        switch (key.ObjectPattern)
        {
            case UpArrow:
                if (currentIndex == -1)
                {
                    currentIndex = history.Count - 1;
                    unsubmittedBuffer.SetContents(contents);
                }
                else if (currentIndex > 0)
                {
                    currentIndex--;
                }

                if (TryGetPreviousMatchingEntry(unsubmittedBuffer, out var matchingPreviousEntryIndex))
                {
                    SetContents(codePane, history[matchingPreviousEntryIndex]);
                    currentIndex = matchingPreviousEntryIndex;
                    key.Handled = true;
                }
                break;
            case DownArrow:
                if (currentIndex != -1)
                {
                    Debug.Assert(currentIndex < history.Count);
                    var result = currentIndex < history.Count - 1 ? history[++currentIndex] : unsubmittedBuffer.ToString();
                    SetContents(codePane, result);
                    key.Handled = true;
                }
                break;
            default:
                currentIndex = -1;
                key.Handled = false;
                break;
        }

        return;
    }

    /// <summary>
    /// Starting at the <see cref="currentIndex"/> node, search backwards for a node
    /// that starts with <paramref name="prefix"/>
    /// </summary>
    private bool TryGetPreviousMatchingEntry(ReadOnlyStringBuilder prefix, out int historyIndex)
    {
        var startIndex = currentIndex == -1 ? history.Count - 1 : currentIndex;
        if (prefix.Length > 0)
        {
            Debug.Assert(currentIndex < history.Count);
            for (int i = startIndex; i >= 0; i--)
            {
                if (history[i].StartsWith(prefix))
                {
                    historyIndex = i;
                    return true;
                }
            }
            historyIndex = -1;
            return false;
        }
        else
        {
            historyIndex = startIndex;
            return true;
        }
    }

    private static void SetContents(CodePane codepane, string contents)
    {
        if (codepane.Document.Equals(contents)) return;

        codepane.Document.SetContents(codepane, contents);
    }

    internal void Track(CodePane codePane)
    {
        var oldDocument = this.codePane?.Document;
        if (oldDocument is not null)
        {
            oldDocument.ClearUndoRedoHistory(); // discard undo/redo history to reduce memory usage
            if (oldDocument.Length > 0)
            {
                var text = oldDocument.GetText();
                if (history.Count == 0 || history[^1] != text) //filter out duplicates
                {
                    history.Add(text);
                }
            }
        }
        currentIndex = -1;
        this.codePane = codePane;
    }

    internal async Task SavePersistentHistoryAsync(string input)
    {
        if (input.Length == 0 || string.IsNullOrEmpty(persistentHistoryFilepath)) return;

        if (history.Count == 0 || history[^1] != input) //filter out duplicates
        {
            var entry = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
            await File.AppendAllLinesAsync(persistentHistoryFilepath, new[] { entry }).ConfigureAwait(false);
        }
    }
}