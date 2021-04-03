using PrettyPrompt.Consoles;
using PrettyPrompt.Panes;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static System.ConsoleKey;

namespace PrettyPrompt.History
{
    class HistoryLog : IKeyPressHandler
    {
        /// <summary>
        /// The actual history, stored as a linked list so we can efficiently go next/prev
        /// </summary>
        private readonly LinkedList<CodePane> history = new LinkedList<CodePane>();

        /// <summary>
        /// The currently active history item. Usually, it's the last element of <see cref="history"/>, unless
        /// the user is navigating next/prev in history.
        /// </summary>
        private LinkedListNode<CodePane> current;

        /// <summary>
        /// In the case the user leaves some text on their prompt, we capture it so we can restore it
        /// when the user stops navigating through history (e.g. by pressing Down Arrow until they're back to their current prompt).
        /// </summary>
        private StringBuilder unsubmittedBuffer;

        public Task OnKeyDown(KeyPress key) => Task.CompletedTask;

        public Task OnKeyUp(KeyPress key)
        {
            if (history.Count == 0 || key.Handled) return Task.CompletedTask;

            switch (key.Pattern)
            {
                case UpArrow when current.Previous is not null:
                    if (current == history.Last)
                    {
                        unsubmittedBuffer = new StringBuilder(history.Last.Value?.Input.ToString());
                    }
                    SetContents(history.Last.Value, current.Previous.Value.Input);
                    current = current.Previous;
                    key.Handled = true;
                    break;
                case DownArrow when current.Next is not null:
                    SetContents(
                        history.Last.Value,
                        current.Next == history.Last && unsubmittedBuffer is not null
                            ? unsubmittedBuffer
                            : current.Next.Value.Input
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

            return Task.CompletedTask;
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
            current = history.AddLast(codePane);
        }
    }
}
