using PrettyPrompt.Consoles;
using System.Collections.Generic;
using System.Threading.Tasks;
using static System.ConsoleKey;
using PrettyPrompt.Panes;
using System.Text;

namespace PrettyPrompt.History
{
    class HistoryLog : IKeyPressHandler
    {
        private readonly LinkedList<CodePane> history = new LinkedList<CodePane>();
        private LinkedListNode<CodePane> current;
        private StringBuilder unsubmittedBuffer;

        public HistoryLog()
        {
        }

        public Task OnKeyDown(KeyPress key) => Task.CompletedTask;

        public Task OnKeyUp(KeyPress key)
        {
            if (history.Count == 0 || key.Handled) return Task.CompletedTask;

            switch (key.Pattern)
            {
                case UpArrow when current.Previous is not null:
                    if(current == history.Last)
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
