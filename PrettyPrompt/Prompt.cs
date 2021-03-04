using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PrettyPrompt.AnsiEscapeCodes;
using static System.ConsoleModifiers;
using static System.ConsoleKey;

namespace PrettyPrompt
{
    public delegate Task<IReadOnlyList<Completion>> CompletionHandlerAsync(string text, int caret);
    public delegate Task<IReadOnlyCollection<FormatSpan>> HighlightHandlerAsync(string text);
    public record PromptResult(bool Success, string Text);
    internal record WrappedLine(int StartIndex, string Content);

    public class Prompt
    {
        private readonly IConsole console;
        private readonly CompletionHandlerAsync complete;
        private readonly HighlightHandlerAsync highlight;

        public Prompt(
            CompletionHandlerAsync completionHandler = null,
            HighlightHandlerAsync highlightHandler = null,
            IConsole console = null)
        {
            this.console = console ?? new SystemConsole();
            this.complete = completionHandler ?? ((_, _) => Task.FromResult<IReadOnlyList<Completion>>(Array.Empty<Completion>()));
            this.highlight = highlightHandler ?? ((_) => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>()));
        }

        public async Task<PromptResult> ReadLine(string prompt)
        {
            StringBuilder input = new StringBuilder();
            console.Write(MoveCursorToColumn(1) + prompt);

            int promptTopCoordinate = console.CursorTop + 1;
            var caret = 0;

            CompletionWindow completionWindow = new CompletionWindow();
            completionWindow.Open(caret);

            while (true)
            {
                int codeAreaWidth = console.BufferWidth - prompt.Length;

                var character = ReadKey();
                var key = character.Modifiers == 0 ? character.Key as object : (character.Modifiers, character.Key);
                bool completionWindowIntercepted = completionWindow.Intercept(key, input, ref caret);
                if (!completionWindowIntercepted)
                {
                    switch (key)
                    {
                        case (Control, C):
                            console.Write("\n" + MoveCursorLeft(codeAreaWidth) + ClearToEndOfScreen);
                            return new PromptResult(false, string.Empty);
                        case (Control, L):
                            console.Clear(); // for some reason, using escape codes (ClearEntireScreen and MoveCursorToPosition) leaves
                                             // CursorTop in an old state. Using Console.Clear() works around this.
                            promptTopCoordinate = 1;
                            break;
                        case (Shift, Enter):
                            input.Insert(caret, '\n');
                            caret++;
                            break;
                        case Enter:
                            console.Write("\n" + MoveCursorToColumn(1) + ClearToEndOfScreen);
                            return new PromptResult(true, input.ToString().EnvironmentNewlines());
                        case Home:
                            caret = 0;
                            break;
                        case Escape:
                            completionWindow.Close();
                            break;
                        case End:
                            caret = input.Length;
                            break;
                        case LeftArrow:
                            caret = Math.Max(0, caret - 1);
                            break;
                        case RightArrow:
                            caret = Math.Min(input.Length, caret + 1);
                            break;
                        case Backspace:
                            if (caret >= 1)
                            {
                                input.Remove(caret - 1, 1);
                                caret--;
                            }
                            break;
                        case Delete:
                            if (caret < input.Length)
                            {
                                input.Remove(caret, 1);
                            }
                            break;
                        case Tab:
                            const int TabWidth = 4; // constant. cannot change. no other possible values could ever make sense.
                            input.Insert(caret, new string(' ', TabWidth));
                            caret += TabWidth;
                            break;
                        case (Control, Spacebar):
                            completionWindow.Open(caret);
                            break;
                        default:
                            if (!char.IsControl(character.KeyChar))
                            {
                                input.Insert(caret, character.KeyChar);
                                caret++;
                            }
                            if (character.KeyChar is '.' or ' ' or '(')
                            {
                                completionWindow.Close();
                                completionWindow.Open(caret);
                            }
                            break;
                    }
                }

                var lines = WordWrap(input, codeAreaWidth, caret, out int cursorRow, out int cursorColumn).ToArray();

                // up and down arrow require operating on word-wrapped output.
                if (!completionWindowIntercepted)
                {
                    if (key is UpArrow && cursorRow > 0)
                    {
                        cursorRow--;
                        var currentLine = lines[cursorRow];
                        cursorColumn = Math.Min(currentLine.Content.TrimEnd().Length, cursorColumn);
                        caret = currentLine.StartIndex + cursorColumn;
                    }
                    else if (key is DownArrow && cursorRow < lines.Length - 1)
                    {
                        cursorRow++;
                        var currentLine = lines[cursorRow];
                        cursorColumn = Math.Min(currentLine.Content.TrimEnd().Length, cursorColumn);
                        caret = currentLine.StartIndex + cursorColumn;
                    }
                }

                int finalCursorRow = promptTopCoordinate + cursorRow;
                int finalCursorColumn = prompt.Length + 1 + cursorColumn;

                if(caret < completionWindow.OpenedIndex)
                {
                    completionWindow.Close();
                }
                
                if(completionWindow.IsOpen)
                {
                    var textToComplete = input.ToString(completionWindow.OpenedIndex, caret - completionWindow.OpenedIndex);
                    if(textToComplete == string.Empty || completionWindow.AllCompletions == CompletionWindow.NeedsCompletions)
                    {
                        var completions = await this.complete.Invoke(input.ToString(), caret);
                        completionWindow.SetCompletions(completions);
                    }
                    else
                    {
                        completionWindow.FilterCompletions(textToComplete);
                    }
                }

                var highlights = await this.highlight.Invoke(input.ToString());

                console.HideCursor();
                console.Write(
                    MoveCursorToPosition(promptTopCoordinate, 1)
                       + ClearToEndOfScreen
                       + string.Concat(lines.Select((line, n) => DrawPrompt(prompt, n) + ApplyHighlighting(highlights, line))).EnvironmentNewlines()
                       + DrawCompletions(completionWindow, input, caret, prompt.Length, codeAreaWidth, finalCursorRow, finalCursorColumn)
                       + MoveCursorToPosition(finalCursorRow, finalCursorColumn)
                );
                console.ShowCursor();
            }
        }

        private static string DrawPrompt(string prompt, int n) =>
            n == 0 ? prompt : new string(' ', prompt.Length);

        private static string ApplyHighlighting(IReadOnlyCollection<FormatSpan> highlights, WrappedLine line)
        {
            var text = new StringBuilder(line.Content);
            foreach (var formatting in highlights.Reverse())
            {
                var lineStart = line.StartIndex;
                var lineEnd = line.StartIndex + text.Length;
                var formattingStart = formatting.Start;
                var formattingEnd = formatting.Start + formatting.Length;
                if(lineStart < formattingEnd && formattingEnd <= lineEnd)
                {
                    text.Insert(formattingEnd - lineStart, ResetFormatting);
                }
                if(lineStart <= formattingStart && formattingStart <= lineEnd)
                {
                    text.Insert(formattingStart - lineStart, ToAnsiEscapeSequence(formatting.Formatting));
                }
            }
            return text.ToString();
        }

        static IReadOnlyCollection<WrappedLine> WordWrap(StringBuilder str, int maxLineSize, int cursorIndex, out int cursorRow, out int cursorCol)
        {
            cursorRow = 0;
            cursorCol = 0;
            if(str.Length == 0)
            {
                cursorCol = cursorIndex;
                return new[] { new WrappedLine(0, string.Empty) };
            }

            var lines = new List<WrappedLine>();
            int currentLineLength = 0;
            var line = new StringBuilder(maxLineSize);
            int textIndex = 0;
            foreach(ReadOnlyMemory<char> chunk in str.GetChunks())
            {
                foreach(char character in chunk.Span)
                {
                    line.Append(character);
                    bool isCursorPastCharacter = cursorIndex > textIndex;

                    currentLineLength++;
                    textIndex++;

                    if (isCursorPastCharacter && !char.IsControl(character))
                    {
                        cursorCol++;
                    }
                    if(character == '\n' || currentLineLength == maxLineSize)
                    {
                        if(isCursorPastCharacter)
                        {
                            cursorRow++;
                            cursorCol = 0;
                        }
                        lines.Add(new WrappedLine(textIndex - currentLineLength, line.ToString()));
                        line = new StringBuilder();
                        currentLineLength = 0;
                    }
                }
            }

            if(currentLineLength > 0)
                lines.Add(new WrappedLine(textIndex - currentLineLength, line.ToString()));

            return lines;
        }

        private static string DrawCompletions(CompletionWindow completionWindow, StringBuilder input, int caret, int codeAreaStartColumn, int codeAreaWidth, int cursorRow, int cursorColumn)
        {
            //  _  <-- cursor location
            //  ┌──────────────┐
            //  │ completion 1 │
            //  │ completion 2 │
            //  └──────────────┘

            if(!completionWindow.IsOpen || caret < completionWindow.OpenedIndex)
                return string.Empty;
            //string typedCompletion = input.ToString(completionWindow.OpenedIndex, caret - completionWindow.OpenedIndex).ToString();
            //if (typedCompletion == string.Empty)
            //    return string.Empty;

            if (completionWindow.FilteredView.Count == 0)
                return string.Empty;

            int wordWidth = completionWindow.FilteredView.Max(w => w.ReplacementText.Length);
            int boxWidth = wordWidth + 2 + 2; // two border characters, plus two spaces for padding
            int boxHeight = completionWindow.FilteredView.Count + 2; // two border characters

            int boxStart =
                boxWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                : cursorColumn + boxWidth >= codeAreaWidth ? codeAreaWidth - boxWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                : cursorColumn; // enough room, we'll show the completion box offset at the cursor location.

            return Blue
                + MoveCursorToPosition(cursorRow + 1, boxStart)
                + "┌" + TruncateToWindow(new string('─', wordWidth + 2), 2) + "┐" + MoveCursorDown(1) + MoveCursorToColumn(boxStart)
                + string.Concat(completionWindow.FilteredView.Select((c,i) =>
                    "│" + (completionWindow.SelectedItem.Value == c ? "|" : " ") + ResetFormatting + TruncateToWindow(c.ReplacementText.PadRight(wordWidth), 4) + Blue + " │" + MoveCursorDown(1) + MoveCursorToColumn(boxStart)
                  ))
                + "└" + TruncateToWindow(new string('─', wordWidth + 2), 2) + "┘" + MoveCursorUp(boxHeight) + MoveCursorToColumn(boxStart)
                + ResetFormatting;

            string TruncateToWindow(string line, int offset) =>
                line.Substring(0, Math.Min(line.Length, codeAreaWidth - boxStart - offset));
        }

        private ConsoleKeyInfo ReadKey()
        {
            while (true)
            {
                if (console.KeyAvailable)
                {
                    return console.ReadKey(intercept: true);
                }
            }
        }
    }
    class CompletionWindow
    {
        /// <summary>
        /// All completions available. Called once when the window is initially opened
        /// </summary>
        public IReadOnlyCollection<Completion> AllCompletions { get; set; } = NeedsCompletions;

        /// <summary>
        /// A "view" over <see cref="AllCompletions"/> that shows the list filtered by what the user has typed.
        /// </summary>
        public LinkedList<Completion> FilteredView { get; set; }
        public LinkedListNode<Completion> SelectedItem { get; set; }
        public int OpenedIndex { get; set; }

        public void SetCompletions(IReadOnlyCollection<Completion> completions)
        {
            AllCompletions = completions;
            FilterCompletions(string.Empty);
        }

        public void FilterCompletions(string filter)
        {
            FilteredView = new LinkedList<Completion>();
            foreach (var completion in AllCompletions)
            {
                if (!Matches(completion, filter)) continue;

                var node = FilteredView.AddLast(completion);
                if (completion.ReplacementText == SelectedItem?.Value.ReplacementText)
                {
                    SelectedItem = node;
                }
            }
            if (SelectedItem is null || !Matches(SelectedItem.Value, filter))
            {
                SelectedItem = FilteredView.First;
            }

            static bool Matches(Completion completion, string filter) =>
                completion.ReplacementText.StartsWith(filter, StringComparison.CurrentCultureIgnoreCase);
        }

        internal bool Intercept(object key, StringBuilder input, ref int caret)
        {
            if (!IsOpen || FilteredView is null || FilteredView.Count == 0 || AllCompletions == NeedsCompletions)
                return false;

            switch(key)
            {
                case DownArrow:
                    var next = SelectedItem.Next;
                    if(next is not null)
                    {
                        SelectedItem = next;
                    }
                    return true;
                case UpArrow:
                    var prev = SelectedItem.Previous;
                    if(prev is not null)
                    {
                        SelectedItem = prev;
                    }
                    return true;
                case Enter:
                case RightArrow:
                case Tab:
                    var completion = SelectedItem.Value;
                    caret = InsertCompletion(input, caret, completion);
                    return true;
                case (Control, Spacebar) when FilteredView.Count == 1:
                    caret = InsertCompletion(input, caret, FilteredView.First.Value);
                    return true;
                default:
                    this.SelectedItem = FilteredView.First;
                    return false;
            }
        }

        private int InsertCompletion(StringBuilder input, int caret, Completion completion)
        {
            input.Remove(completion.StartIndex, caret - completion.StartIndex);
            input.Insert(completion.StartIndex, completion.ReplacementText);
            Close();
            return completion.StartIndex + completion.ReplacementText.Length;
        }

        public bool IsOpen { get; set; }
        internal void Open(int caret)
        {
            IsOpen = true;
            this.OpenedIndex = caret;
            AllCompletions = NeedsCompletions;
        }

        internal void Close()
        {
            this.IsOpen = false;
            this.OpenedIndex = int.MinValue;
            this.SelectedItem = null;
        }

        public static readonly LinkedList<Completion> NeedsCompletions = new LinkedList<Completion>();
    }
    public class Completion
    {
        public int StartIndex { get; set; }
        public string ReplacementText { get; set; }
    }
}
