using PrettyPrompt.Consoles;
using PrettyPrompt.Panes;
using System;
using System.Linq;
using System.Threading.Tasks;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.TextSelection
{
    class SelectionKeyPressHandler : IKeyPressHandler
    {
        private readonly CodePane codePane;
        private ConsoleCoordinate previousCursorLocation;

        public SelectionKeyPressHandler(CodePane codePane)
        {
            this.codePane = codePane;
        }

        public Task OnKeyDown(KeyPress key)
        {
            this.previousCursorLocation = codePane.Cursor;

            if(key.Pattern is (Control, A))
            {
                codePane.Caret = codePane.Input.Length;
            }
            return Task.CompletedTask;
        }

        public Task OnKeyUp(KeyPress key)
        {
            var selection = codePane.Selection;
            var cursor = codePane.Cursor;

            if (key.Pattern is (Control, A))
            {
                selection.Clear();
                var start = new ConsoleCoordinate(0, 0);
                var end = new ConsoleCoordinate(codePane.WordWrappedLines.Count, codePane.WordWrappedLines.Last().Content.Length);
                selection.Add(new SelectionSpan(start, end));
                return Task.CompletedTask;
            }

            var (anchor, selectionCursor) = key.Pattern switch
            {
                (Shift, LeftArrow) => (previousCursorLocation.Clone(columnOffset: -1), cursor.Clone()),
                (Shift, RightArrow) => (previousCursorLocation, cursor.Clone(columnOffset: -1)),
                (Shift, UpArrow) => (previousCursorLocation, cursor.Clone()),
                (Shift, DownArrow) => (previousCursorLocation, cursor.Clone()),
                _ => (null, null)
            };

            if (anchor is null) // keypress is not related to selection
            {
                selection.Clear();
                return Task.CompletedTask;
            }

            if(selection.Count == 0)
            {
                selection.Add(new SelectionSpan(anchor, selectionCursor));
                return Task.CompletedTask;
            }

            foreach (var select in selection)
            {
                select.Cursor = key.Pattern is (Shift, RightArrow) && ShouldCursorLeadSelection(cursor, select)
                    ? cursor.Clone()
                    : selectionCursor;
            }

            return Task.CompletedTask;
        }

        private static bool ShouldCursorLeadSelection(ConsoleCoordinate cursor, SelectionSpan select) =>
            select.Anchor.Row > cursor.Row || (select.Anchor.Row == cursor.Row && select.Anchor.Column >= cursor.Column);
    }
}
