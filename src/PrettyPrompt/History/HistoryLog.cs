#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Panes;

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
    private string unsubmittedBuffer = string.Empty;

    /// <summary>
    /// Filepath of the history storage file. If null, history is not saved. History is stored as base64 encoded lines,
    /// so we can efficiently append to the file, and not have to worry about newlines in the history entries.
    /// </summary>
    private readonly string? persistentHistoryFilepath;
    private readonly KeyBindings keyBindings;
    private readonly Task loadPersistentHistoryTask;

    /// <summary>
    /// Indices of path through history. Sequence can contain gaps because of filtering but it should be monotonously decreasing.
    /// </summary>
    private readonly Stack<int> historyPath = new();

    private bool historyEntryWasUsedLastTime;

    /// <summary>
    /// The currently code pane being edited. The contents of this pane will be changed when
    /// navigating through the history.
    /// </summary>
    private CodePane? codePane;

    public HistoryLog(string? persistentHistoryFilepath, KeyBindings keyBindings)
    {
        this.persistentHistoryFilepath = persistentHistoryFilepath;
        this.keyBindings = keyBindings;
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
            if(TryBase64Decode(line, out var lineOfCode))
            {
                history.Add(lineOfCode);
            }
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
        var allowInMultilineStatement = historyEntryWasUsedLastTime;
        historyEntryWasUsedLastTime = false;
        if (codePane is null) return;

        await loadPersistentHistoryTask.ConfigureAwait(false);

        if (history.Count == 0 || key.Handled) return;

        var contents = codePane.Document.GetText();
        if (contents.Contains('\n') && !allowInMultilineStatement)
        {
            //we do not want to cycle in history in multiline documents
            return;
        }

        if (keyBindings.HistoryPrevious.Matches(key.ConsoleKeyInfo))
        {
            int startIndex = -1;
            if (historyPath.Count == 0)
            {
                startIndex = history.Count - 1;
                unsubmittedBuffer = contents;
            }
            else if (historyPath.Peek() > 0)
            {
                startIndex = historyPath.Peek() - 1;
            }

            if (startIndex != -1)
            {
                if (TryGetPreviousMatchingEntryIndex(unsubmittedBuffer, startIndex, out var matchingPreviousEntryIndex))
                {
                    SetContents(codePane, history[matchingPreviousEntryIndex]);
                    historyEntryWasUsedLastTime = true;
                    historyPath.Push(matchingPreviousEntryIndex);
                    key.Handled = true;
                }
            }
        }
        else if (keyBindings.HistoryNext.Matches(key.ConsoleKeyInfo))
        {
            if (historyPath.Count > 0)
            {
                historyPath.Pop();
                if (historyPath.Count > 0)
                {
                    SetContents(codePane, history[historyPath.Peek()]);
                    historyEntryWasUsedLastTime = true;
                }
                else
                {
                    SetContents(codePane, unsubmittedBuffer);
                }
                key.Handled = true;
            }
        }
        else
        {
            historyPath.Clear();
            key.Handled = false;
        }
    }

    private bool TryGetPreviousMatchingEntryIndex(string pattern, int startIndex, out int historyIndex)
    {
        Debug.Assert(startIndex >= 0);
        Debug.Assert(startIndex < history.Count);

        if (!string.IsNullOrEmpty(pattern))
        {
            if (TryGet(out historyIndex, static (entry, pattern) => entry.StartsWith(pattern, StringComparison.Ordinal))) return true;
            if (TryGet(out historyIndex, static (entry, pattern) => entry.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))) return true;
            if (TryGet(out historyIndex, static (entry, pattern) => entry.Contains(pattern, StringComparison.Ordinal))) return true;
            if (TryGet(out historyIndex, static (entry, pattern) => entry.Contains(pattern, StringComparison.OrdinalIgnoreCase))) return true;
        }

        return TryGet(out historyIndex, static (entry, pattern) => true);

        bool TryGet(out int historyIndex, Func<string, string, bool> isMatch)
        {
            for (int i = startIndex; i >= 0; i--)
            {
                if (isMatch(history[i], pattern) &&
                    history[i] != pattern) //we do not want to return the same entry as is alearedy written
                {
                    historyIndex = i;
                    return true;
                }
            }
            historyIndex = -1;
            return false;
        }
    }

    private static void SetContents(CodePane codepane, string contents)
    {
        if (codepane.Document.Equals(contents)) return;

        codepane.Document.SetContents(codepane, contents, caret: contents.Length);
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
        historyPath.Clear();
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

    private static bool TryBase64Decode(string line, [NotNullWhen(true)] out string? decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(line));
            return true;
        }
        catch (FormatException)
        {
            // Don't consider invalid history entries a hard-failure. Invalid lines
            // will eventually be trimmed once we pass MaxHistoryEntries.
            decoded = null;
            return false;
        }
    }
}