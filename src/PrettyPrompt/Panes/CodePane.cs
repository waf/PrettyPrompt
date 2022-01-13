#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.TextSelection;
using TextCopy;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Panes;

internal class CodePane : IKeyPressHandler
{
    private readonly ForceSoftEnterCallbackAsync shouldForceSoftEnterAsync;
    private readonly SelectionKeyPressHandler selectionHandler;
    private int topCoordinate;
    private int codeAreaWidth = int.MaxValue;
    private int codeAreaHeight = int.MaxValue;
    private int windowTop;

    /// <summary>
    /// The input text being edited in the pane
    /// </summary>
    public Document Document { get; }

    /// <summary>
    /// The final input text that was entered into the pane.
    /// When null, the text is still being edited.
    /// </summary>
    public PromptResult? Result { get; private set; }

    public int TopCoordinate
    {
        get => topCoordinate;
        private set
        {
            Debug.Assert(value >= 0);
            topCoordinate = value;
        }
    }

    public int CodeAreaWidth
    {
        get => codeAreaWidth;
        private set
        {
            Debug.Assert(value >= 0);
            codeAreaWidth = value;
        }
    }

    public int CodeAreaHeight
    {
        get => codeAreaHeight;
        private set
        {
            Debug.Assert(value >= 0);
            codeAreaHeight = value;
        }
    }

    public SelectionSpan? Selection { get; set; }

    /// <summary>
    /// Document text split into lines.
    /// </summary>
    public IReadOnlyList<WrappedLine> WordWrappedLines { get; private set; }

    /// <summary>
    /// The two-dimensional coordinate of the text cursor in the document,
    /// after word wrapping / newlines have been processed.
    /// </summary>
    public ConsoleCoordinate Cursor { get; set; }

    public CodePane(int topCoordinate, ForceSoftEnterCallbackAsync shouldForceSoftEnterAsync)
    {
        this.TopCoordinate = topCoordinate;
        this.shouldForceSoftEnterAsync = shouldForceSoftEnterAsync;
        this.Document = new Document();
        this.selectionHandler = new SelectionKeyPressHandler(this);

        WordWrap();
    }

    public async Task OnKeyDown(KeyPress key)
    {
        if (key.Handled) return;

        await this.selectionHandler.OnKeyDown(key);
        var selection = GetSelectionStartEnd();
        switch (key.Pattern)
        {
            case (Control, C) when selection is null:
                Result = new PromptResult(IsSuccess: false, string.Empty, IsHardEnter: false);
                break;
            case (Control, L):
                TopCoordinate = 0; // actually clearing the screen is handled in the renderer.
                break;
            case Enter when await shouldForceSoftEnterAsync(Document.GetText()):
            case (Shift, Enter):
                Document.InsertAtCaret('\n', selection);
                break;
            case (Control, Enter):
            case (Control | Alt, Enter):
                Result = new PromptResult(IsSuccess: true, Document.GetText().EnvironmentNewlines(), IsHardEnter: true);
                break;
            case Enter:
                Result = new PromptResult(IsSuccess: true, Document.GetText().EnvironmentNewlines(), IsHardEnter: false);
                break;
            case Home or (Shift, Home):
                Document.MoveToLineBoundary(-1);
                break;
            case End or (Shift, End):
                Document.MoveToLineBoundary(+1);
                break;
            case (Control, Home) or (Control | Shift, Home):
                Document.Caret = 0;
                break;
            case (Control, End) or (Control | Shift, End):
                Document.Caret = Document.Length;
                break;
            case (Shift, LeftArrow):
            case LeftArrow:
                Document.Caret = Math.Max(0, Document.Caret - 1);
                break;
            case (Shift, RightArrow):
            case RightArrow:
                Document.Caret = Math.Min(Document.Length, Document.Caret + 1);
                break;
            case (Control | Shift, LeftArrow):
            case (Control, LeftArrow):
                Document.MoveToWordBoundary(-1);
                break;
            case (Control | Shift, RightArrow):
            case (Control, RightArrow):
                Document.MoveToWordBoundary(+1);
                break;
            case (Control, Backspace) when selection is null:
                var startDeleteIndex = Document.CalculateWordBoundaryIndexNearCaret(-1);
                Document.Remove(startDeleteIndex, Document.Caret - startDeleteIndex);
                break;
            case (Control, Delete) when selection is null:
                var endDeleteIndex = Document.CalculateWordBoundaryIndexNearCaret(+1);
                Document.Remove(Document.Caret, endDeleteIndex - Document.Caret);
                break;
            case Backspace when selection is null:
                Document.Remove(Document.Caret - 1, 1);
                break;
            case Delete when selection is null:
                Document.Remove(Document.Caret, 1);
                break;
            case (_, Delete) or (_, Backspace) or Delete or Backspace when selection.TryGet(out var selectionValue):
                {
                    Document.DeleteSelectedText(selectionValue.Start, selectionValue.End);
                }
                break;
            case Tab:
                Document.InsertAtCaret("    ", selection);
                break;
            case (Control, X) when selection.TryGet(out var selectionValue):
                {
                    var cutContent = Document.GetText(selectionValue.Start, selectionValue.End - selectionValue.Start);
                    Document.Remove(selectionValue.Start, selectionValue.End - selectionValue.Start);
                    await ClipboardService.SetTextAsync(cutContent);
                    break;
                }
            case (Control, X):
                {
                    await ClipboardService.SetTextAsync(Document.GetText());
                    break;
                }
            case (Control, C) when selection.TryGet(out var selectionValue):
                {
                    var copiedContent = Document.GetText(selectionValue.Start, selectionValue.End - selectionValue.Start);
                    await ClipboardService.SetTextAsync(copiedContent);
                    break;
                }
            case (Control | Shift, C):
                await ClipboardService.SetTextAsync(Document.GetText());
                break;
            case (Shift, Insert) when key.PastedText is not null:
                PasteText(key.PastedText);
                break;
            case (Control, V):
            case (Control | Shift, V):
            case (Shift, Insert):
                var clipboardText = await ClipboardService.GetTextAsync();
                PasteText(clipboardText);
                break;
            case (Control, Z):
                Document.Undo();
                WordWrap();
                break;
            case (Control, Y):
                Document.Redo();
                WordWrap();
                break;
            default:
                if (!char.IsControl(key.ConsoleKeyInfo.KeyChar))
                {
                    Document.InsertAtCaret(key.ConsoleKeyInfo.KeyChar, selection);
                }
                break;
        }
    }

    public (int Start, int End)? GetSelectionStartEnd()
    {
        return Selection.TryGet(out var selectionSpanValue) ? selectionSpanValue.GetCaretIndices(WordWrappedLines) : default((int Start, int End)?);
    }

    private void PasteText(string? pastedText)
    {
        if (string.IsNullOrEmpty(pastedText)) return;

        string dedentedText = DedentMultipleLines(pastedText);
        this.Document.InsertAtCaret(dedentedText, GetSelectionStartEnd());
    }

    internal void MeasureConsole(IConsole console, string prompt)
    {
        var windowTopChange = console.WindowTop - this.windowTop;
        this.TopCoordinate = Math.Max(0, this.TopCoordinate - windowTopChange);
        this.windowTop = console.WindowTop;

        this.CodeAreaWidth = Math.Max(0, console.BufferWidth - prompt.Length);
        this.CodeAreaHeight = Math.Max(0, console.WindowHeight - this.TopCoordinate);
    }

    public async Task OnKeyUp(KeyPress key)
    {
        if (key.Handled) return;

        switch (key.Pattern)
        {
            case (Shift, UpArrow) when Cursor.Row > 0:
            case UpArrow when Cursor.Row > 0:
                {
                    var newCursor = Cursor.MoveUp();
                    var aboveLine = WordWrappedLines[newCursor.Row];
                    Cursor = newCursor.WithColumn(Math.Min(aboveLine.Content.AsSpan().TrimEnd().Length, newCursor.Column));
                    Document.Caret = aboveLine.StartIndex + Cursor.Column;
                    key.Handled = true;
                    break;
                }
            case (Shift, DownArrow) when Cursor.Row < WordWrappedLines.Count - 1:
            case DownArrow when Cursor.Row < WordWrappedLines.Count - 1:
                {
                    var newCursor = Cursor.MoveDown();
                    var belowLine = WordWrappedLines[newCursor.Row];
                    Cursor = newCursor.WithColumn(Math.Min(belowLine.Content.AsSpan().TrimEnd().Length, newCursor.Column));
                    Document.Caret = belowLine.StartIndex + Cursor.Column;
                    key.Handled = true;
                    break;
                }
        }

        await selectionHandler.OnKeyUp(key);
    }

    [MemberNotNull(nameof(WordWrappedLines))]
    public void WordWrap()
    {
        (WordWrappedLines, Cursor) = Document.WrapEditableCharacters(CodeAreaWidth);
    }

    /// <summary>
    /// If we have text with consistent, leading indentation, trim that indentation ("dedent" it).
    /// This handles the scenario where users are pasting from an IDE.
    /// </summary>
    private static string DedentMultipleLines(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
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