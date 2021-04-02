using PrettyPrompt.Consoles;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Panes
{
    public record WrappedLine(int StartIndex, string Content);

    public delegate Task<bool> ForceSoftEnterHandlerAsync(string text);

    internal class CodePane : IKeyPressHandler
    {
        private readonly ForceSoftEnterHandlerAsync shouldForceSoftEnterAsync;

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

        public CodePane(int topCoordinate, ForceSoftEnterHandlerAsync shouldForceSoftEnterAsync)
        {
            this.TopCoordinate = topCoordinate;
            this.shouldForceSoftEnterAsync = shouldForceSoftEnterAsync;
            this.Caret = 0;
            this.Input = new StringBuilder();
        }

        public async Task OnKeyDown(KeyPress key)
        {
            if (key.Handled) return;

            switch (key.Pattern)
            {
                case (Control, C):
                    Result = new PromptResult(false, string.Empty);
                    break;
                case (Control, L):
                    TopCoordinate = 0; // actually clearing the screen is handled in the renderer.
                    break;
                case Enter when await shouldForceSoftEnterAsync(Input.ToString()):
                case (Shift, Enter):
                    Input.Insert(Caret, '\n');
                    Caret++;
                    break;
                case Enter:
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
                case (Control, LeftArrow):
                    Caret = CalculateWordBoundaryIndex(Input, Caret, -1);
                    break;
                case (Control, RightArrow):
                    Caret = CalculateWordBoundaryIndex(Input, Caret, +1);
                    break;
                case (Control, Backspace):
                    var startDeleteIndex = CalculateWordBoundaryIndex(Input, Caret, -1);
                    Input.Remove(startDeleteIndex, Caret - startDeleteIndex);
                    Caret = startDeleteIndex;
                    break;
                case (Control, Delete):
                    var endDeleteIndex = CalculateWordBoundaryIndex(Input, Caret, +1);
                    Input.Remove(Caret, endDeleteIndex - Caret);
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
            if (Input.Length == 0)
            {
                Cursor.Column = Caret;
                WordWrappedLines = new[] { new WrappedLine(0, string.Empty) };
                return;
            }

            var lines = new List<WrappedLine>();
            int currentLineLength = 0;
            var line = new StringBuilder(CodeAreaWidth);
            int textIndex = 0;
            foreach (ReadOnlyMemory<char> chunk in Input.GetChunks())
            {
                foreach (char character in chunk.Span)
                {
                    line.Append(character);
                    bool isCursorPastCharacter = Caret > textIndex;

                    currentLineLength++;
                    textIndex++;

                    if (isCursorPastCharacter && !char.IsControl(character))
                    {
                        Cursor.Column++;
                    }
                    if (character == '\n' || currentLineLength == CodeAreaWidth)
                    {
                        if (isCursorPastCharacter)
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

            if (currentLineLength > 0)
                lines.Add(new WrappedLine(textIndex - currentLineLength, line.ToString()));

            WordWrappedLines = lines;
        }

        private static int CalculateWordBoundaryIndex(StringBuilder input, int caret, int direction)
        {
            int bound = direction > 0 ? input.Length : 0;

            if (Math.Abs(caret - bound) <= 2)
                return bound;

            for (var i = caret + direction; bound == 0 ? i > 0 : i < bound - 1; i += direction)
            {
                int c1Index = i + (direction > 0 ? 0 : -1);
                int c2Index = i + (direction > 0 ? 1 : 0);
                if (IsWordStart(input[c1Index], input[c2Index]))
                    return c2Index;
            }

            bool IsWordStart(char c1, char c2) => !char.IsLetterOrDigit(c1) && char.IsLetterOrDigit(c2);

            return bound;
        }

    }
}
