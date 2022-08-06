#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Panes;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.TextSelection;

internal class SelectionKeyPressHandler : IKeyPressHandler
{
    private readonly CodePane codePane;
    private ConsoleCoordinate previousCursorLocation;

    public SelectionKeyPressHandler(CodePane codePane)
    {
        this.codePane = codePane;
    }

    public Task OnKeyDown(KeyPress key, CancellationToken cancellationToken)
    {
        this.previousCursorLocation = codePane.Cursor;

        if (key.ObjectPattern is (Control, A))
        {
            codePane.Document.Caret = codePane.Document.Length;
        }
        return Task.CompletedTask;
    }

    public Task OnKeyUp(KeyPress key, CancellationToken cancellationToken)
    {
        switch (key.ObjectPattern)
        {
            case (Control, C):
                {
                    // as a special case, even though Ctrl+C isn't related to selection, it should keep the current selected text.
                    return Task.CompletedTask;
                }
            case (Control, A):
                {
                    var start = ConsoleCoordinate.Zero;
                    var end = new ConsoleCoordinate(codePane.WordWrappedLines.Count - 1, codePane.WordWrappedLines[^1].Content.Length);
                    if (start < end)
                    {
                        codePane.Selection = new SelectionSpan(start, end, SelectionDirection.FromLeftToRight);
                    }
                    return Task.CompletedTask;
                }
            case
                (Control, Z) or
                (Control, Y):
                {
                    //do not reset codePane.Selection
                    return Task.CompletedTask;
                }
            case
                (Shift, End) or
                (Shift, Home) or

                (Control | Shift, End) or
                (Control | Shift, Home) or

                (Shift, RightArrow) or
                (Shift, UpArrow) or

                (Control | Shift, RightArrow) or
                (Control | Shift, UpArrow) or

                (Shift, DownArrow) or
                (Shift, LeftArrow) or

                (Control | Shift, DownArrow) or
                (Control | Shift, LeftArrow):
                {
                    var cursor = codePane.Cursor;
                    if (previousCursorLocation < cursor)
                    {
                        UpdateSelection(new SelectionSpan(previousCursorLocation, cursor, SelectionDirection.FromLeftToRight));
                    }
                    else if (previousCursorLocation > cursor)
                    {
                        UpdateSelection(new SelectionSpan(cursor, previousCursorLocation, SelectionDirection.FromRightToLeft));
                    }
                    break;
                }
            case LeftWindows or RightWindows or F1 or F2 or F3 or F4 or F5 or F6 or F7 or F8 or F9 or F10 or F11 or F12:
                return Task.CompletedTask;
            case Tab or (Shift, Tab):
                {
                    //see CodePane Tab / (Shift, Tab) handling - we need to preserve selection sometimes
                    if (codePane.GetSelectionSpan().TryGet(out var selectionValue))
                    {
                        var isMultilineSelection = codePane.Document.GetText(selectionValue).Contains('\n');
                        if (isMultilineSelection)
                        {
                            return Task.CompletedTask;
                        }
                        else
                        {
                            codePane.Selection = null;
                        }
                    }
                    break;
                }
            default:
                // keypress is not related to selection
                codePane.Selection = null;
                break;
        }

        return Task.CompletedTask;

        void UpdateSelection(SelectionSpan newSelection)
        {
            if (codePane.Selection.TryGet(out var selection))
            {
                codePane.Selection = selection.GetUpdatedSelection(newSelection);
            }
            else
            {
                codePane.Selection = newSelection;
            }
        }
    }
}
