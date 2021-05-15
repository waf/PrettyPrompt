using PrettyPrompt.Consoles;
using PrettyPrompt.Panes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.ConsoleKey;

namespace PrettyPrompt.History
{
    sealed class HistoryLog : IKeyPressHandler
    {
        private const int MaxHistoryEntries = 500;
        private const int HistoryTrimInterval = 100;

        /// <summary>
        /// The actual history, stored as a linked list so we can efficiently go next/prev
        /// </summary>
        private readonly LinkedList<StringBuilder> history = new LinkedList<StringBuilder>();

        /// <summary>
        /// The currently active history item. Usually, it's the last element of <see cref="history"/>, unless
        /// the user is navigating next/prev in history.
        /// </summary>
        private LinkedListNode<StringBuilder> current;

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
        private StringBuilder unsubmittedBuffer;

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
                history.AddFirst(new StringBuilder(entry));
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
                        unsubmittedBuffer = new StringBuilder(history.Last.Value?.ToString());
                    }
                    SetContents(latestCodePane, current.Previous.Value);
                    current = current.Previous;
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

        private static void SetContents(CodePane codepane, StringBuilder contents)
        {
            codepane.Input.Clear();
            codepane.Input.Append(contents);
            codepane.Caret = contents.Length;
            codepane.WordWrap();
        }

        internal void Track(CodePane codePane)
        {
            current = history.AddLast(codePane.Input);
            latestCodePane = codePane;
        }

        internal async Task SavePersistentHistoryAsync(StringBuilder input)
        {
            if (input.Length == 0 || string.IsNullOrEmpty(persistentHistoryFilepath)) return;

            var entry = Convert.ToBase64String(Encoding.UTF8.GetBytes(input.ToString()));
            await File.AppendAllLinesAsync(persistentHistoryFilepath, new[] { entry }).ConfigureAwait(false);
        }
    }
}
