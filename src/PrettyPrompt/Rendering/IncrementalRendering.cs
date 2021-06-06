#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using System;
using System.Text;
using static PrettyPrompt.Consoles.AnsiEscapeCodes;

namespace PrettyPrompt.Rendering
{
    static class IncrementalRendering
    {
        /// <summary>
        /// Given a new screen and the previously rendered screen,
        /// returns the minimum required ansi escape sequences to
        /// render the new screen.
        /// 
        /// In the simple case, where the user typed a single character, we should only return that character (e.g. the returned string will be of length 1).
        /// A more complicated case, like finishing a word that triggers syntax highlighting, we should redraw just that word in the new color.
        /// An even more complicated case, like opening the autocomplete menu, should draw the autocomplete menu, and return the cursor to the correct position.
        /// </summary>
        public static string CalculateDiff(Screen currentScreen, Screen previousScreen, ConsoleCoordinate ansiCoordinate, ConsoleCoordinate cursor)
        {
            var diff = new StringBuilder();

            // if there are multiple characters with the same formatting, don't output formatting
            // instructions per character; instead output one instruction at the beginning for all
            // characters that share the same formatting.
            ConsoleFormat currentFormatRun = null;
            var previousCoordinate = new ConsoleCoordinate(
                row: ansiCoordinate.Row + previousScreen.Cursor.Row,
                column: ansiCoordinate.Column + previousScreen.Cursor.Column
            );

            // for "latin" characters, where each character is a single character-width, this horizontalRenderPosition isn't strictly needed.
            // however, for CJK character, each character is two character-widths, so we need to track "index of the character we're rendering" and
            // the "coordinate at which we're rendering" separately.
            int currentHorizontalRenderPosition = 0;
            int previousHorizontalRenderPosition = 0;
            foreach (var (i, currentCell, previousCell) in currentScreen.CellBuffer.ZipLongest(previousScreen.CellBuffer))
            {
                if(i % currentScreen.Width == 0)
                {
                    currentHorizontalRenderPosition = 0;
                    previousHorizontalRenderPosition = 0;
                }

                previousHorizontalRenderPosition += previousCell?.CellWidth ?? 1;
                if(currentCell == previousCell)
                {
                    currentHorizontalRenderPosition += currentCell?.CellWidth ?? 1;
                    continue;
                }

                var cellCoordinate = new ConsoleCoordinate(
                    row: ansiCoordinate.Row + (i / currentScreen.Width),
                    column: ansiCoordinate.Column + currentHorizontalRenderPosition
                );

                MoveCursorIfRequired(diff, previousCoordinate, cellCoordinate);
                previousCoordinate.Row = cellCoordinate.Row;
                previousCoordinate.Column = cellCoordinate.Column;

                // Part 1: handle when we're erasing characters/formatting from the previously rendered screen.
                if (currentCell?.Formatting == null)
                {
                    if (currentFormatRun is not null)
                    {
                        diff.Append(Reset);
                        currentFormatRun = null;
                    }

                    if (currentCell?.Text is null || (currentCell.Text == "\n" && previousCell is not null)) // no character, or a newline which should erase the cell in which it was typed.
                    {
                        var eraseWidth = previousHorizontalRenderPosition - currentHorizontalRenderPosition;
                        if(eraseWidth > 0)
                        {
                            diff.Append(' ', eraseWidth);
                            UpdateCoordinateFromCursorMove(previousScreen, ansiCoordinate, diff, previousCoordinate, previousCell, eraseWidth);

                            if (currentCell is null)
                            {
                                currentHorizontalRenderPosition += eraseWidth;
                            }
                        }
                    }
                    if (currentCell is null)
                    {
                        continue;
                    }
                }

                // Part 2: write out current character, with any formatting
                if (currentCell.Formatting != currentFormatRun)
                {
                    diff.Append(
                        ToAnsiEscapeSequence(currentCell.Formatting)
                        + currentCell.Text
                    );
                    currentFormatRun = currentCell.Formatting;
                }
                else
                {
                    diff.Append(currentCell.Text);
                }

                // Part 3: update internal tracking of cursor movement.
                // writing to the console will automatically move the cursor.
                // update our internal tracking so we calculate the least
                // amount of movement required for the next character.
                if (currentCell.Text == "\n")
                {
                    UpdateCoordinateFromNewLine(previousCoordinate);
                }
                else
                {
                    UpdateCoordinateFromCursorMove(currentScreen, ansiCoordinate, diff, previousCoordinate, currentCell, currentCell.CellWidth);
                }
                currentHorizontalRenderPosition += currentCell.CellWidth;
            }

            if (currentFormatRun is not null)
            {
                diff.Append(Reset);
            }

            // all done rendering, update the cursor position if we need to. If we rendered the
            // autocomplete menu, or if the cursor is manually positioned in the middle of
            // the text, the cursor won't be in the correct position.
            MoveCursorIfRequired(diff, fromCoordinate: previousCoordinate, toCoordinate: new ConsoleCoordinate(
                cursor.Row + ansiCoordinate.Row,
                cursor.Column + ansiCoordinate.Column
            ));

            return diff.ToString();
        }

        private static void UpdateCoordinateFromCursorMove(
            Screen currentScreen, ConsoleCoordinate ansiCoordinate, StringBuilder diff, ConsoleCoordinate previousCoordinate, Cell currentCell, int moveCount)
        {
            // if we hit the edge of the screen, wrap
            bool hitRightEdgeOfScreen = previousCoordinate.Column + moveCount == currentScreen.Width + ansiCoordinate.Column;
            if (hitRightEdgeOfScreen)
            {
                if(currentCell is not null && !currentCell.TruncateToScreenHeight)
                {
                    diff.Append('\n');
                    UpdateCoordinateFromNewLine(previousCoordinate);
                }
            }
            else
            {
                previousCoordinate.Column += moveCount;
            }
        }

        private static void UpdateCoordinateFromNewLine(ConsoleCoordinate previousCoordinate)
        {
            // for simplicity, we standardize all newlines to "\n" regardless of platform. However, that complicates our diff,
            // because "\n" on windows _only_ moves one line down, it does not change the column. Handle that here.
            previousCoordinate.Row++;
            if (!OperatingSystem.IsWindows())
            {
                previousCoordinate.Column = 1;
            }
        }

        private static void MoveCursorIfRequired(StringBuilder diff, ConsoleCoordinate fromCoordinate, ConsoleCoordinate toCoordinate)
        {
            // we only ever move the cursor relative to its current position.
            // this is because ansi escape sequences know nothing about the current scroll in the window,
            // they only operate on the current viewport. If we move to absolute positions, the display
            // is garbled if the user scrolls the window and then types.

            if (fromCoordinate.Row != toCoordinate.Row)
            {
                diff.Append(fromCoordinate.Row < toCoordinate.Row
                    ? MoveCursorDown(toCoordinate.Row - fromCoordinate.Row)
                    : MoveCursorUp(fromCoordinate.Row - toCoordinate.Row)
                );
            }
            if (fromCoordinate.Column != toCoordinate.Column)
            {
                diff.Append(fromCoordinate.Column < toCoordinate.Column
                    ? MoveCursorRight(toCoordinate.Column - fromCoordinate.Column)
                    : MoveCursorLeft(fromCoordinate.Column - toCoordinate.Column)
                );
            }
        }
    }
}
