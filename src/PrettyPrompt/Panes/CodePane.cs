#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextCopy;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Panes
{
    public record WrappedLine(int StartIndex, string Content);

    internal class CodePane : IKeyPressHandler
    {
        private readonly ForceSoftEnterCallbackAsync shouldForceSoftEnterAsync;

        // dimensions
        public int TopCoordinate { get; set; }
        public int CodeAreaWidth { get; set; }
        public int CodeAreaHeight { get; private set; }
        public int WindowTop { get; private set; }

        // input/output
        public StringBuilder Input { get; }
        public int Caret { get; set; }
        public PromptResult Result { get; private set; }

        // word wrapping
        public IReadOnlyList<WrappedLine> WordWrappedLines { get; private set; }
        public ConsoleCoordinate Cursor { get; private set; }

        public CodePane(int topCoordinate, ForceSoftEnterCallbackAsync shouldForceSoftEnterAsync)
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
                    Result = new PromptResult(IsSuccess: false, string.Empty, IsHardEnter: false);
                    break;
                case (Control, L):
                    TopCoordinate = 0; // actually clearing the screen is handled in the renderer.
                    break;
                case Enter when await shouldForceSoftEnterAsync(Input.ToString()):
                case (Shift, Enter):
                    Input.Insert(Caret, '\n');
                    Caret++;
                    break;
                case (Control, Enter):
                case (Control | Alt, Enter):
                    Result = new PromptResult(IsSuccess: true, Input.ToString().EnvironmentNewlines(), IsHardEnter: true);
                    break;
                case Enter:
                    Result = new PromptResult(IsSuccess: true, Input.ToString().EnvironmentNewlines(), IsHardEnter: false);
                    break;
                case Home:
                    Caret = CalculateLineBoundaryIndex(Input, Caret, -1);
                    break;
                case End:
                    Caret = CalculateLineBoundaryIndex(Input, Caret, +1);
                    break;
                case (Control, Home):
                    Caret = 0;
                    break;
                case (Control, End):
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
                case (Control | Shift, C):
                    await ClipboardService.SetTextAsync(Input.ToString());
                    break;
                case (Shift, Insert) when key.PastedText is not null:
                    PasteText(key.PastedText);
                    break;
                case (Control, V):
                case (Control | Shift, V):
                case (Shift, Insert):
                    string clipboardText = await ClipboardService.GetTextAsync();
                    PasteText(clipboardText);
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

        private void PasteText(string pastedText)
        {
            if (string.IsNullOrEmpty(pastedText)) return;

            string dedentedText = DedentMultipleLines(pastedText);
            Input.Insert(Caret, dedentedText);
            Caret += dedentedText.Length;
        }

        internal void MeasureConsole(IConsole console, string prompt)
        {
            this.TopCoordinate -= (console.WindowTop - this.WindowTop);
            this.WindowTop = console.WindowTop;

            this.CodeAreaWidth = console.BufferWidth - prompt.Length;
            this.CodeAreaHeight = console.WindowHeight - this.TopCoordinate;
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

        public void WordWrap() =>
            (WordWrappedLines, Cursor) = WordWrapping.WrapEditableCharacters(Input, Caret, CodeAreaWidth);

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

            static bool IsWordStart(char c1, char c2) => !char.IsLetterOrDigit(c1) && char.IsLetterOrDigit(c2);

            return bound;
        }

        private static int CalculateLineBoundaryIndex(StringBuilder input, int caret, int direction)
        {
            if (input.Length == 0) return caret;

            if (direction == +1 && caret < input.Length && input[caret] == '\n') return caret;

            int bound = direction > 0 ? input.Length : 0;

            for (var i = caret + direction; bound == 0 ? i > 0 : i < bound; i += direction)
            {
                if (input[i] == '\n')
                    return i + (direction == -1 ? 1 : 0);
            }

            return bound;
        }

        /// <summary>
        /// If we have text with consistent, leading indentation, trim that indentation ("dedent" it).
        /// This handles the scenario where users are pasting from an IDE.
        /// </summary>
        private static string DedentMultipleLines(string text)
        {
            var lines = text.Split(new[] { '\r', '\n' });
            if (lines.Length > 1)
            {
                var nonEmptyLines = lines
                    .Where(line => line != string.Empty)
                    .ToList();

                if (!nonEmptyLines.Any())
                    return text;

                var leadingIndent = nonEmptyLines
                    .Select(line => line.TakeWhile(char.IsWhiteSpace).Count())
                    .Min();

                for (var i = 0; i < lines.Length; i++)
                {
                    lines[i] = lines[i].Substring(Math.Min(lines[i].Length, leadingIndent));
                }
            }
            var pastedText = string.Join('\n', lines);
            return pastedText;
        }
    }
}
