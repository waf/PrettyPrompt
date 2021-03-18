using PrettyPrompt.Consoles;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static PrettyPrompt.AnsiEscapeCodes;
using static System.ConsoleModifiers;
using static System.ConsoleKey;

namespace PrettyPrompt
{
    public record WrappedLine(int StartIndex, string Content);
    public class Coordinate
    {
        public int Row { get; set; }
        public int Column { get; set; }
    }

    public class CodePane : IKeyPressHandler
    {
        private readonly IConsole console;

        // dimensions
        public int TopCoordinate { get; private set; }
        public int CodeAreaWidth { get; set; }

        // input/output
        public StringBuilder Input { get; }
        public int Caret { get; set; }
        public PromptResult Result { get; private set; }

        // word wrapping
        public IReadOnlyList<WrappedLine> WordWrappedLines { get; private set; }
        public Coordinate Cursor { get; private set; }

        public CodePane(IConsole console)
        {
            this.console = console;
            this.TopCoordinate = console.CursorTop + 1; // ansi escape coordinates are 1-indexed.
            this.Caret = 0;
            this.Input = new StringBuilder();
        }

        public Task<bool> OnKeyDown(KeyPress key)
        {
            switch (key.Pattern)
            {
                case (Control, C):
                    console.Write("\n" + MoveCursorToColumn(1) + ClearToEndOfScreen);
                    Result = new PromptResult(false, string.Empty);
                    break;
                case (Control, L):
                    console.Clear(); // for some reason, using escape codes (ClearEntireScreen and MoveCursorToPosition) leaves
                                     // CursorTop in an old state. Using Console.Clear() works around this.
                    TopCoordinate = 1;
                    break;
                case (Shift, Enter):
                    Input.Insert(Caret, '\n');
                    Caret++;
                    break;
                case Enter:
                    console.Write("\n" + MoveCursorToColumn(1) + ClearToEndOfScreen);
                    Result = new PromptResult(true, Input.ToString().EnvironmentNewlines());
                    break;
                case Home:
                    Caret = 0;
                    break;
                case End:
                    Caret = Input.Length;
                    break;
                case LeftArrow:
                    Caret = Math.Max(0, Caret - 1);
                    break;
                case RightArrow:
                    Caret = Math.Min(Input.Length, Caret + 1);
                    break;
                case Backspace:
                    if (Caret >= 1)
                    {
                        Input.Remove(Caret - 1, 1);
                        Caret--;
                    }
                    break;
                case Delete:
                    if (Caret < Input.Length)
                    {
                        Input.Remove(Caret, 1);
                    }
                    break;
                case Tab:
                    Input.Insert(Caret, "    ");
                    Caret += 4;
                    break;
                default:
                    if (!char.IsControl(key.ConsoleKeyInfo.KeyChar))
                    {
                        Input.Insert(Caret, key.ConsoleKeyInfo.KeyChar);
                        Caret++;
                    }
                    break;
            }
            return Task.FromResult(true);
        }

        public Task<bool> OnKeyUp(KeyPress key)
        {
            switch (key.Pattern)
            {
                case UpArrow when Cursor.Row > 0:
                    Cursor.Row--;
                    var aboveLine = WordWrappedLines[Cursor.Row];
                    Cursor.Column = Math.Min(aboveLine.Content.TrimEnd().Length, Cursor.Column);
                    Caret = aboveLine.StartIndex + Cursor.Column;
                    break;
                case DownArrow when Cursor.Row < WordWrappedLines.Count - 1:
                    Cursor.Row++;
                    var belowLine = WordWrappedLines[Cursor.Row];
                    Cursor.Column = Math.Min(belowLine.Content.TrimEnd().Length, Cursor.Column);
                    Caret = belowLine.StartIndex + Cursor.Column;
                    break;
            }
            return Task.FromResult(true);
        }

        public void WordWrap()
        {
            Cursor = new Coordinate();
            if(Input.Length == 0)
            {
                Cursor.Column = Caret;
                WordWrappedLines = new[] { new WrappedLine(0, string.Empty) };
                return;
            }

            var lines = new List<WrappedLine>();
            int currentLineLength = 0;
            var line = new StringBuilder(CodeAreaWidth);
            int textIndex = 0;
            foreach(ReadOnlyMemory<char> chunk in Input.GetChunks())
            {
                foreach(char character in chunk.Span)
                {
                    line.Append(character);
                    bool isCursorPastCharacter = Caret > textIndex;

                    currentLineLength++;
                    textIndex++;

                    if (isCursorPastCharacter && !char.IsControl(character))
                    {
                        Cursor.Column++;
                    }
                    if(character == '\n' || currentLineLength == CodeAreaWidth)
                    {
                        if(isCursorPastCharacter)
                        {
                            Cursor.Row++;
                            Cursor.Column = 0;
                        }
                        lines.Add(new WrappedLine(textIndex - currentLineLength, line.ToString()));
                        line = new StringBuilder();
                        currentLineLength = 0;
                    }
                }
            }

            if(currentLineLength > 0)
                lines.Add(new WrappedLine(textIndex - currentLineLength, line.ToString()));

            WordWrappedLines = lines;
        }
    }
}
