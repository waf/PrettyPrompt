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
        /// </summary>
        public static string RenderDiff(Screen currentScreen, Screen previousScreen, ConsoleCoordinate ansiCoordinate, ConsoleCoordinate cursor)
        {
            var diff = new StringBuilder();
            var maxIndex = Math.Max(currentScreen.MaxIndex, previousScreen.MaxIndex);

            // if there are multiple characters with the same formatting, don't output formatting
            // instructions per character; instead output one instruction at the beginning for all
            // characters that share the same formatting.
            ConsoleFormat currentFormatRun = null;
            var previousCoordinate = new ConsoleCoordinate(
                row: ansiCoordinate.Row + previousScreen.Cursor.Row,
                column: ansiCoordinate.Column + previousScreen.Cursor.Column
            );

            for (var i = 0; i < maxIndex; i++)
            {
                Cell currentCell = i < currentScreen.CharBuffer.Length ? currentScreen.CharBuffer[i] : null;
                Cell previousCell = i < previousScreen.CharBuffer.Length ? previousScreen.CharBuffer[i] : null;
                var cellCoordinate = new ConsoleCoordinate(
                    row: ansiCoordinate.Row + i / currentScreen.Width,
                    column: ansiCoordinate.Column + i % currentScreen.Width
                );

                if (currentCell != previousCell)
                {
                    MoveCursorIfRequired(diff, previousCoordinate, cellCoordinate);
                    previousCoordinate.Row = cellCoordinate.Row;
                    previousCoordinate.Column = cellCoordinate.Column;

                    // handle when we're erasing characters/formatting from the previously rendered screen.
                    if (currentCell?.Formatting == null)
                    {
                        if (currentFormatRun is not null)
                        {
                            diff.Append(ResetFormatting);
                            currentFormatRun = null;
                        }

                        if (currentCell?.Text is null || currentCell.Text == "\n")
                        {
                            diff.Append(' ');
                            UpdateCoordinateFromCursorMove(currentScreen, ansiCoordinate, diff, previousCoordinate);

                            if (currentCell is null)
                            {
                                continue;
                            }
                        }
                    }

                    // write out current character, with any formatting
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

                    // writing to the console will automatically move the cursor.
                    // update our internal tracking so we calculate the least
                    // amount of movement required for the next character.
                    if (currentCell.Text == "\n")
                    {
                        UpdateCoordinateFromNewLine(previousCoordinate);
                    }
                    else
                    {
                        UpdateCoordinateFromCursorMove(currentScreen, ansiCoordinate, diff, previousCoordinate);
                    }
                }
            }

            var finalCursorPosition = new ConsoleCoordinate(
                cursor.Row + ansiCoordinate.Row,
                cursor.Column + ansiCoordinate.Column
            );

            MoveCursorIfRequired(diff, previousCoordinate, finalCursorPosition);

            if (currentFormatRun is not null)
            {
                diff.Append(ResetFormatting);
            }
            return diff.ToString();
        }

        private static void UpdateCoordinateFromCursorMove(Screen currentScreen, ConsoleCoordinate ansiCoordinate, StringBuilder diff, ConsoleCoordinate previousCoordinate)
        {
            // if we hit the edge of the screen, wrap
            if (previousCoordinate.Column + 1 == currentScreen.Width + ansiCoordinate.Column)
            {
                diff.Append('\n');
                UpdateCoordinateFromNewLine(previousCoordinate);
            }
            else
            {
                previousCoordinate.Column++;
            }
        }

        private static void UpdateCoordinateFromNewLine(ConsoleCoordinate previousCoordinate)
        {
            // for simplicity, we standardize all newlines to "\n" regardless of platform. However, that complicates
            // our diff, because "\n" on windows _only_ moves one line down, it does not change the column.
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
