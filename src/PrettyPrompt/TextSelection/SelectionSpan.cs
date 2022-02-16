#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Diagnostics;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;

namespace PrettyPrompt.TextSelection;

internal readonly record struct SelectionSpan
{
    /// <summary>
    /// Inclusive.
    /// </summary>
    public readonly ConsoleCoordinate Start;

    /// <summary>
    /// Exclusive.
    /// </summary>
    public readonly ConsoleCoordinate End;

    public readonly SelectionDirection Direction;

    public SelectionSpan(ConsoleCoordinate start, ConsoleCoordinate end, SelectionDirection direction)
    {
        Debug.Assert(start >= ConsoleCoordinate.Zero);
        Debug.Assert(start < end);

        Start = start;
        End = end;
        Direction = direction;
    }

    public TextSpan GetCaretIndices(IReadOnlyList<WrappedLine> wrappedLines)
    {
        Debug.Assert(Start.Column <= wrappedLines[Start.Row].Content.Length);
        //End.Row==wrappedLines.Count when last line ends with '\n' (-> End.Row is empty and not present in wrappedLines)
        Debug.Assert(End.Row <= wrappedLines.Count);
        Debug.Assert(End.Column <= (End.Row < wrappedLines.Count ? wrappedLines[End.Row].Content.Length : 0));

        var selectionStart = wrappedLines[Start.Row].StartIndex + Start.Column;
        var selectionEnd =
            End.Row < wrappedLines.Count ?
            wrappedLines[End.Row].StartIndex + End.Column :
            wrappedLines[End.Row - 1].StartIndex + wrappedLines[End.Row - 1].Content.Length;

        Debug.Assert(selectionStart < selectionEnd);
        return TextSpan.FromBounds(selectionStart, selectionEnd);
    }

    public SelectionSpan WithStart(ConsoleCoordinate start) => new(start, End, Direction);
    public SelectionSpan WithEnd(ConsoleCoordinate end) => new(Start, end, Direction);

    public SelectionSpan? GetUpdatedSelection(SelectionSpan newSelection)
    {
        if (Direction == SelectionDirection.FromLeftToRight)
        {
            if (newSelection.Direction == SelectionDirection.FromLeftToRight)
            {
                Debug.Assert(newSelection.End > End);
                return ChangeEnd(this, newSelection.End);
            }
            else
            {
                Debug.Assert(newSelection.End == End);
                return ChangeEnd(this, newSelection.Start);
            }

            static SelectionSpan? ChangeEnd(SelectionSpan selection, ConsoleCoordinate end)
            {
                if (end > selection.Start)
                {
                    return selection.WithEnd(end);
                }
                else
                {
                    return
                        end == selection.Start ?
                        null : //cancelation of selection
                        new(end, selection.Start, SelectionDirection.FromRightToLeft); //change of selection direction
                }
            }
        }
        else
        {
            if (newSelection.Direction == SelectionDirection.FromLeftToRight)
            {
                Debug.Assert(newSelection.Start == Start);
                return ChangeStart(this, newSelection.End);
            }
            else
            {
                Debug.Assert(newSelection.End == Start);
                return ChangeStart(this, newSelection.Start);
            }

            static SelectionSpan? ChangeStart(SelectionSpan selection, ConsoleCoordinate start)
            {
                if (start < selection.End)
                {
                    return selection.WithStart(start);
                }
                else
                {
                    return
                        start == selection.End ?
                        null : //cancelation of selection
                        new(selection.End, start, SelectionDirection.FromLeftToRight); //change of selection direction
                }
            }
        }
    }

    public override string ToString() => $"Start: ({Start}), End: ({End})";
}

public enum SelectionDirection
{
    FromLeftToRight,
    FromRightToLeft
}
