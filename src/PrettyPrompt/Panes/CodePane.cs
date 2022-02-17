#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Rendering;
using PrettyPrompt.TextSelection;
using TextCopy;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Panes;

internal class CodePane : IKeyPressHandler
{
    private readonly PromptConfiguration configuration;
    private readonly IPromptCallbacks promptCallbacks;
    private readonly IClipboard clipboard;
    private readonly SelectionKeyPressHandler selectionHandler;
    private int topCoordinate;
    private int codeAreaWidth = int.MaxValue;
    private int codeAreaHeight = int.MaxValue;
    private int windowTop;
    private WordWrappedText wordWrappedText;

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
    public IReadOnlyList<WrappedLine> WordWrappedLines => wordWrappedText.WrappedLines;

    /// <summary>
    /// The two-dimensional coordinate of the text cursor in the document,
    /// after word wrapping / newlines have been processed.
    /// </summary>
    public ConsoleCoordinate Cursor
    {
        get => wordWrappedText.Cursor;
        set => wordWrappedText.Cursor = value;
    }

    public string TabSpaces { get; }

    public CodePane(int topCoordinate, PromptConfiguration configuration, IPromptCallbacks promptCallbacks, IClipboard clipboard)
    {
        this.TopCoordinate = topCoordinate;
        this.configuration = configuration;
        this.promptCallbacks = promptCallbacks;
        this.clipboard = clipboard;
        this.Document = new Document();
        this.Document.Changed += WordWrap;
        this.selectionHandler = new SelectionKeyPressHandler(this);
        TabSpaces = new string(' ', configuration.TabSize);

        WordWrap();

        void WordWrap() => wordWrappedText = Document.WrapEditableCharacters(CodeAreaWidth);
    }

    public async Task OnKeyDown(KeyPress key)
    {
        if (key.Handled) return;

        await selectionHandler.OnKeyDown(key).ConfigureAwait(false);
        var selection = GetSelectionSpan();

        if (await promptCallbacks.InterpretKeyPressAsInputSubmitAsync(Document.GetText(), Document.Caret, key.ConsoleKeyInfo).ConfigureAwait(false))
        {
            Result = new PromptResult(isSuccess: true, Document.GetText().EnvironmentNewlines(), key.ConsoleKeyInfo);
            return;
        }

        switch (key.ObjectPattern)
        {
            case (Control, C) when selection is null:
                Result = new PromptResult(isSuccess: false, string.Empty, key.ConsoleKeyInfo);
                break;
            case (Control, L):
                TopCoordinate = 0; // actually clearing the screen is handled in the renderer.
                break;
            case var _ when configuration.KeyBindings.NewLine.Matches(key.ConsoleKeyInfo):
                Document.InsertAtCaret(this, '\n');
                break;
            case var _ when configuration.KeyBindings.SubmitPrompt.Matches(key.ConsoleKeyInfo):
                Result = new PromptResult(isSuccess: true, Document.GetText().EnvironmentNewlines(), key.ConsoleKeyInfo);
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
                Document.Remove(this, startDeleteIndex, Document.Caret - startDeleteIndex);
                break;
            case (Control, Delete) when selection is null:
                var endDeleteIndex = Document.CalculateWordBoundaryIndexNearCaret(+1);
                Document.Remove(this, Document.Caret, endDeleteIndex - Document.Caret);
                break;
            case Backspace when selection is null:
                Document.Remove(this, Document.Caret - 1, 1);
                break;
            case Delete when selection is null:
                Document.Remove(this, Document.Caret, 1);
                break;
            case (_, Delete) or (_, Backspace) or Delete or Backspace when selection.HasValue:
                {
                    Document.DeleteSelectedText(this);
                }
                break;
            case Tab:
                Document.InsertAtCaret(this, TabSpaces);
                break;
            case (Control, X) when selection.TryGet(out var selectionValue):
                {
                    var cutContent =  Document.GetText(selectionValue).ToString();
                    Document.Remove(this, selectionValue);
                    await clipboard.SetTextAsync(cutContent).ConfigureAwait(false);
                    break;
                }
            case (Control, X):
                {
                    await clipboard.SetTextAsync(Document.GetText()).ConfigureAwait(false);
                    break;
                }
            case (Control, C) when selection.TryGet(out var selectionValue):
                {
                    var copiedContent = Document.GetText(selectionValue).ToString();
                    await clipboard.SetTextAsync(copiedContent).ConfigureAwait(false);
                    break;
                }
            case (Control | Shift, C):
                await clipboard.SetTextAsync(Document.GetText()).ConfigureAwait(false);
                break;
            case (Shift, Insert) when key.PastedText is not null:
                PasteText(key.PastedText);
                break;
            case (Control, V):
            case (Control | Shift, V):
            case (Shift, Insert):
                var clipboardText = await clipboard.GetTextAsync().ConfigureAwait(false);
                PasteText(clipboardText);
                break;
            case (Control, Z):
                Document.Undo(out var newSelection);
                Selection = newSelection;
                break;
            case (Control, Y):
                Document.Redo(out newSelection);
                Selection = newSelection;
                break;
            default:
                if (!char.IsControl(key.ConsoleKeyInfo.KeyChar))
                {
                    Document.InsertAtCaret(this, key.ConsoleKeyInfo.KeyChar);
                }
                break;
        }
    }

    public TextSpan? GetSelectionSpan()
    {
        if (Selection.TryGet(out var selection))
        {
            var selectionSpan = selection.GetCaretIndices(WordWrappedLines);
            Debug.Assert(new TextSpan(0, Document.Length).Contains(selectionSpan));
            return selectionSpan;
        }
        return default;
    }

    private void PasteText(string? pastedText)
    {
        if (string.IsNullOrEmpty(pastedText)) return;

        var filteredText = DedentMultipleLinesAndFilter(pastedText);
        this.Document.InsertAtCaret(this, filteredText);

        //If we have text with consistent, leading indentation, trim that indentation ("dedent" it).
        //This handles the scenario where users are pasting from an IDE.
        //Also replaces tabs as spaces and filtrs out special characters.
        string DedentMultipleLinesAndFilter(string text)
        {
            var sb = new StringBuilder();
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length > 1)
            {
                var nonEmptyLines = lines
                    .Where(line => line.Length > 0)
                    .ToList();

                if (!nonEmptyLines.Any())
                {
                    return text;
                }

                var leadingIndent = nonEmptyLines
                    .Select(line => line.TakeWhile(char.IsWhiteSpace).Count())
                    .Min();

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Substring(Math.Min(lines[i].Length, leadingIndent));
                    AppendFiltered(sb, line);
                    if (i != lines.Length - 1) sb.Append('\n');
                }
            }
            else
            {
                AppendFiltered(sb, lines[0]);
            }
            return sb.ToString();
        }

        void AppendFiltered(StringBuilder sb, string line)
        {
            foreach (var c in line)
            {
                if (c == '\t')
                {
                    sb.Append(TabSpaces);
                }
                else
                {
                    if (UnicodeWidth.GetWidth(c) >= 1)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        continue;
                    }
                }
            }
        }
    }

    internal void MeasureConsole(IConsole console, int promptLength)
    {
        var windowTopChange = console.WindowTop - this.windowTop;
        this.TopCoordinate = Math.Max(0, this.TopCoordinate - windowTopChange);
        this.windowTop = console.WindowTop;

        this.CodeAreaWidth = Math.Max(0, console.BufferWidth - promptLength);
        this.CodeAreaHeight = Math.Max(0, console.WindowHeight - this.TopCoordinate);
    }

    public async Task OnKeyUp(KeyPress key)
    {
        if (key.Handled) return;

        switch (key.ObjectPattern)
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

        await selectionHandler.OnKeyUp(key).ConfigureAwait(false);

        CheckConsistency();
    }

    [Conditional("DEBUG")]
    private void CheckConsistency()
    {
        if (Selection.TryGet(out var selection))
        {
            var selectionSpan = selection.GetCaretIndices(WordWrappedLines);
            Debug.Assert(Document.Caret >= selectionSpan.Start);
            Debug.Assert(Document.Caret <= selectionSpan.End);
            Debug.Assert(Cursor >= selection.Start);
            Debug.Assert(Cursor <= selection.End);
        }
    }
}