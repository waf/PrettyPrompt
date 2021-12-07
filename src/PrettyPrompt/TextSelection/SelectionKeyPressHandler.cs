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
            if (key.Pattern is (Control, C))

            {
                // as a special case, even though Ctrl+C isn't related to selection, it should keep the current selected text.
                return Task.CompletedTask;
            }

            if (key.Pattern is (Control, A))
            {
                var start = ConsoleCoordinate.Zero;
                var end = new ConsoleCoordinate(document.WordWrappedLines.Count - 1, document.WordWrappedLines[^1].Content.Length);
                if (start < end)
                {
                    document.Selection = new SelectionSpan(start, end, SelectionDirection.FromLeftToRight);
                }
                return Task.CompletedTask;
            }

            var cursor = document.Cursor;
            switch (key.Pattern)
            {
                case (Shift, End) or
                    (Control | Shift, End) or
                    (Shift, RightArrow) or
                    (Control | Shift, RightArrow) or
                    (Shift, DownArrow) or
                    (Control | Shift, DownArrow):
                    {
                        if (previousCursorLocation < cursor)
                        {
                            UpdateSelection(new SelectionSpan(previousCursorLocation, cursor, SelectionDirection.FromLeftToRight));
                        }
                        break;
                    }

                case (Shift, Home) or
                    (Control | Shift, Home) or
                    (Shift, UpArrow) or
                    (Control | Shift, UpArrow) or
                    (Shift, LeftArrow) or
                    (Control | Shift, LeftArrow):
                    {
                        if (cursor < previousCursorLocation)
                        {
                            UpdateSelection(new SelectionSpan(cursor, previousCursorLocation, SelectionDirection.FromRightToLeft));
                        }
                        break;
                    }

                default:
                    // keypress is not related to selection
                    document.Selection = null;
                    break;
            }

            return Task.CompletedTask;

            void UpdateSelection(SelectionSpan newSelection)
            {
                if (document.Selection.TryGet(out var selection))
                {
                    document.Selection = selection.GetUpdatedSelection(newSelection);
                }
                else
                {
                    document.Selection = newSelection;
                }
            }
        }
    }
}
