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

            if (key.Pattern is (Control, C))
            {
                // as a special case, even though Ctrl+C isn't related to selection, it should keep the current selected text.
                return Task.CompletedTask;
            }

            if (key.Pattern is (Control, A))
            {
                var start = new ConsoleCoordinate(0, 0);
                var end = new ConsoleCoordinate(document.WordWrappedLines.Count - 1, document.WordWrappedLines[^1].Content.Length);
                document.Selection = new SelectionSpan(start, end);
                return Task.CompletedTask;
            }

            (ConsoleCoordinate Anchor, ConsoleCoordinate SelectionCursor)? anchorWithSelectionCursorNullable =
                key.Pattern switch
                {
                    (Shift, End) or
                    (Control | Shift, End) or
                    (Shift, RightArrow) or
                    (Control | Shift, RightArrow) or
                    (Shift, DownArrow) or
                    (Control | Shift, DownArrow)
                        => (previousCursorLocation, cursor.MoveLeft()),

                    (Shift, Home) or
                    (Control | Shift, Home) or
                    (Shift, UpArrow) or
                    (Control | Shift, UpArrow) or
                    (Shift, LeftArrow) or
                    (Control | Shift, LeftArrow)
                        => (previousCursorLocation.MoveLeft(), cursor),

                    _ => null
                };

            if (anchorWithSelectionCursorNullable.TryGet(out var anchorWithSelectionCursor))
            {
                var (anchor, selectionCursor) = anchorWithSelectionCursor;
                if (selection.TryGet(out var selectionValue))
                {
                    var newCursor = key.Pattern is (Shift, RightArrow) && ShouldCursorLeadSelection(cursor, selectionValue.Anchor)
                      ? cursor
                      : selectionCursor;
                    document.Selection = new SelectionSpan(selectionValue.Anchor, newCursor);
                }
                else
                {
                    document.Selection = new SelectionSpan(anchor, selectionCursor);
                }
            }
            else
            {
                // keypress is not related to selection
                document.Selection = null;
            }
            return Task.CompletedTask;
        }

        private static bool ShouldCursorLeadSelection(ConsoleCoordinate cursor, ConsoleCoordinate selectionAnchor) =>
                    selectionAnchor.Row > cursor.Row || (selectionAnchor.Row == cursor.Row && selectionAnchor.Column >= cursor.Column);
    }
}
