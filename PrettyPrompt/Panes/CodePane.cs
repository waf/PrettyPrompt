using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static System.ConsoleModifiers;
using static System.ConsoleKey;
using PrettyPrompt.Consoles;

namespace PrettyPrompt.Panes
{
    public record WrappedLine(int StartIndex, string Content);

    public class CodePane : IKeyPressHandler
    {
        // dimensions
        public int TopCoordinate { get; private set; }
        public int CodeAreaWidth { get; set; }

        // input/output
        public StringBuilder Input { get; }
        public int Caret { get; set; }
        public PromptResult Result { get; private set; }

        // word wrapping
        public IReadOnlyList<WrappedLine> WordWrappedLines { get; private set; }
        public ConsoleCoordinate Cursor { get; private set; }

        public CodePane(int topCoordinate)
        {
            this.TopCoordinate = topCoordinate;
            this.Caret = 0;
            this.Input = new StringBuilder();
        }

        public Task OnKeyDown(KeyPress key)
        {
            if (key.Handled) return Task.CompletedTask;

            switch (key.Pattern)
            {
                case (Control, C):
                    Result = new PromptResult(false, string.Empty);
                    break;
                case (Control, L):
                    TopCoordinate = 0; // actually clearing the screen is handled in the renderer.
                    break;
                case (Shift, Enter):
                    Input.Insert(Caret, '\n');
                    Caret++;
                    break;
                case Enter:
                    Result = new PromptResult(true,   Input.ToString().EnvironmentNewlines());
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
                case (Control, LeftArrow):
                    Caret = CalculatePreviousWordBoundary(Input, Caret, -1);
                    break;
                case (Control, RightArrow):
                    Caret = CalculatePreviousWordBoundary(Input, Caret, +1);
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

            return Task.CompletedTask;
        }

        public Task OnKeyUp(KeyPress key)
        {
            if (key.Handled) return Task.CompletedTask;

            switch (key.Pattern)
            {
                case UpArrow when Cursor.Row > 0:
                    Cursor.Row--;
                    var aboveLine = WordWrappedLines[Cursor.Row];
                    Cursor.Column = Math.Min(aboveLine.Content.TrimEnd().Length, Cursor.Column);
                    Caret = aboveLine.StartIndex + Cursor.Column;
                    key.Handled = true;
                    break;
                case DownArrow when Cursor.Row < WordWrappedLines.Count - 1:
                    Cursor.Row++;
                    var belowLine = WordWrappedLines[Cursor.Row];
                    Cursor.Column = Math.Min(belowLine.Content.TrimEnd().Length, Cursor.Column);
                    Caret = belowLine.StartIndex + Cursor.Column;
                    key.Handled = true;
                    break;
            }

            return Task.CompletedTask;
        }

        public void WordWrap()
        {
            Cursor = new ConsoleCoordinate();
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

        private static int CalculatePreviousWordBoundary(StringBuilder input, int caret, int direction)
        {
            int bound = direction > 0 ? input.Length : 0;

            if (input.Length <= 2 || caret == bound)
                return bound;

            bool initialWordCharacter = char.IsLetterOrDigit(input[caret + direction * 2]);
            for (var i = caret + direction * 2; bound == 0 ? i > 0 : i < bound; i += direction)
            {
                if (char.IsLetterOrDigit(input[i]) != initialWordCharacter)
                    return i - direction;
            }

            return bound;
        }

    }
}
