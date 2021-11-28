#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.TextSelection
{
    class SelectionKeyPressHandler : IKeyPressHandler
    {
        private readonly Document document;
        private ConsoleCoordinate previousCursorLocation;

        public SelectionKeyPressHandler(Document document)
        {
            this.document = document;
        }

        public Task OnKeyDown(KeyPress key)
        {
            this.previousCursorLocation = document.Cursor;

            if (key.Pattern is (Control, A))
            {
                document.Caret = document.Length;
            }
            return Task.CompletedTask;
        }

        public Task OnKeyUp(KeyPress key)
        {
            var selection = document.Selection;
            var cursor = document.Cursor;

            // as a special case, even though Ctrl+C isn't related to selection, it should keep the current selected text.
            if (key.Pattern is (Control, C)) return Task.CompletedTask;

            if (key.Pattern is (Control, A))
            {
                selection.Clear();
                var start = new ConsoleCoordinate(0, 0);
                var end = new ConsoleCoordinate(document.WordWrappedLines.Count - 1, document.WordWrappedLines[^1].Content.Length);
                selection.Add(new SelectionSpan(start, end));
                return Task.CompletedTask;
            }

            var (anchor, selectionCursor) = key.Pattern switch
            {
                (Shift, End) or
                (Control | Shift, End) or
                (Shift, RightArrow) or
                (Control | Shift, RightArrow) or
                (Shift, DownArrow) or
                (Control | Shift, DownArrow)
                    => (previousCursorLocation.Clone(), cursor.Clone(columnOffset: -1)),

                (Shift, Home) or
                (Control | Shift, Home) or
                (Shift, UpArrow) or
                (Control | Shift, UpArrow) or
                (Shift, LeftArrow) or
                (Control | Shift, LeftArrow)
                    => (previousCursorLocation.Clone(columnOffset: -1), cursor.Clone()),

                _ => (null, null)
            };

            if (anchor is null) // keypress is not related to selection
            {
                selection.Clear();
                return Task.CompletedTask;
            }

            if (selection.Count == 0)
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
