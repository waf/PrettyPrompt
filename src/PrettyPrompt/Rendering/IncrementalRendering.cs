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
            var max = Math.Max(currentScreen.MaxIndex, previousScreen.MaxIndex);

            // if there are multiple characters with the same formatting, don't output formatting
            // instructions per character; instead output one instruction at the beginning for all
            // characters that share the same formatting.
            ConsoleFormat currentFormatRun = null;
            int previousCoordinateRow = ansiCoordinate.Row + previousScreen.Cursor.Row;
            int previousCoordinateColumn = ansiCoordinate.Column + previousScreen.Cursor.Column;

            for(var i = 0; i < max; i++)
            {
                Cell currentCell = currentScreen.CharBuffer[i];
                if(currentCell != previousScreen.CharBuffer[i])
                {
                    // position cursor, if we need to.
                    var cellCoordinateRow = ansiCoordinate.Row + i / currentScreen.Width;
                    var cellCoordinateColumn = ansiCoordinate.Column + i % currentScreen.Width;
                    if (cellCoordinateColumn != previousCoordinateColumn || cellCoordinateRow != previousCoordinateRow)
                    {
                        diff.Append(MoveCursorToPosition(cellCoordinateRow, cellCoordinateColumn));
                    }
                    previousCoordinateColumn = cellCoordinateColumn;
                    previousCoordinateRow = cellCoordinateRow;

                    // handle when we're erasing previous characters/formatting
                    if(currentCell?.Formatting == null)
                    {
                        if(currentFormatRun is not null)
                        {
                            diff.Append(ResetFormatting);
                            currentFormatRun = null;
                        }

                        if(currentCell == null)
                        {
                            diff.Append(' ');
                            continue;
                        }
                    }

                    var character = currentCell.Text == "\n"
                        ? " " // newlines should clear the cell they're written to.
                        : currentCell.Text;

                    // write out current character, with any formatting
                    if(currentCell.Formatting != currentFormatRun)
                    {
                        diff.Append(
                            ToAnsiEscapeSequence(currentCell.Formatting)
                            + character
                        );
                        currentFormatRun = currentCell.Formatting;
                    }
                    else
                    {
                        diff.Append(character);
                    }
                    if(!string.IsNullOrWhiteSpace(currentCell.Text))
                    {
                        previousCoordinateColumn++;
                    }
                }
            }

            var pos = new ConsoleCoordinate(
                cursor.Row + ansiCoordinate.Row,
                cursor.Column + ansiCoordinate.Column
            );
            // if the diff is a only a single character, no need to update the cursor position because printing
            // the character moves the cursor position for us. Prevents overly verbose ansi escape sequences.
            if(diff.Length != 1 || pos.Row != previousCoordinateRow || pos.Column != previousCoordinateColumn)
            {
                diff.Append(MoveCursorToPosition(pos));
            }
            if(currentFormatRun is not null)
            {
                diff.Append(ResetFormatting);
            }
            return diff.ToString();
        }
    }
}
