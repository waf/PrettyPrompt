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
using System.Threading;
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
    private readonly IConsole console;
    private readonly PromptConfiguration configuration;
    private readonly IClipboard clipboard;
    private readonly SelectionKeyPressHandler selectionHandler;
    private int topCoordinate;
    private int codeAreaWidth;
    private int codeAreaHeight;
    private int windowTop;
    private WordWrappedText wordWrappedText;
    private CompletionPane completionPane = null!;
    private OverloadPane overloadPane = null!;

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

    public int EmptySpaceAtBottomOfWindowHeight
    {
        get
        {
            var overloadPaneMin = OverloadPane.MinHeight;

            var completionPaneMin = BoxDrawing.VerticalBordersHeight + configuration.MinCompletionItemsCount;
            var completionPaneMax = BoxDrawing.VerticalBordersHeight + configuration.MaxCompletionItemsCount;

            var min = Math.Min(overloadPaneMin, completionPaneMin);
            var max = completionPaneMax;

            return ((int)(configuration.ProportionOfWindowHeightForCompletionPane * console.WindowHeight)).Clamp(min, max);
        }
    }

    public CodePane(IConsole console, PromptConfiguration configuration, IClipboard clipboard)
    {
        this.console = console;
        this.configuration = configuration;
        this.clipboard = clipboard;

        TopCoordinate = console.CursorTop;
        MeasureConsole();

        Document = new Document();
        Document.Changed += WordWrap;
        selectionHandler = new SelectionKeyPressHandler(this);
        TabSpaces = new string(' ', configuration.TabSize);

        WordWrap();

        void WordWrap() => wordWrappedText = Document.WrapEditableCharacters(CodeAreaWidth);
    }

    internal void Bind(CompletionPane completionPane, OverloadPane overloadPane)
    {
        this.completionPane = completionPane;
        this.overloadPane = overloadPane;
    }

    public async Task OnKeyDown(KeyPress key, CancellationToken cancellationToken)
    {
        if (key.Handled) return;

        await selectionHandler.OnKeyDown(key, cancellationToken).ConfigureAwait(false);
        var selection = GetSelectionSpan();

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
            case Tab or (Shift, Tab):
                {
                    var shift = key.ConsoleKeyInfo.Modifiers.HasFlag(Shift);
                    if (selection.TryGet(out var selectionValue))
                    {
                        var isMultilineSelection = Document.GetText(selectionValue).Contains('\n');
                        if (isMultilineSelection)
                        {
                            Document.Indent(this, selectionValue, direction: shift ? -1 : 1);
                            CheckConsistency();
                            break;
                        }
                    }
                    if (!shift) Document.InsertAtCaret(this, TabSpaces);
                }
                break;
            case (Control, X) when selection.TryGet(out var selectionValue):
                {
                    var cutContent = Document.GetText(selectionValue).ToString();
                    Selection = null;
                    Document.Remove(this, selectionValue);
                    await clipboard.SetTextAsync(cutContent, cancellationToken).ConfigureAwait(false);
                    break;
                }
            case (Control, X) or (Shift, Delete):
                {
                    var lineStart = Document.CalculateLineBoundaryIndexNearCaret(-1, smartHome: false);
                    var lineEnd = Document.CalculateLineBoundaryIndexNearCaret(1, smartHome: false);
                    var span = TextSpan.FromBounds(lineStart, lineEnd);
                    if (span.End < Document.Length)
                    {
                        Debug.Assert(Document.GetText()[span.End] == '\n');
                        span = new TextSpan(span.Start, span.Length + 1);
                    }

                    if (key.ObjectPattern is (Control, X))
                    {
                        await clipboard.SetTextAsync(Document.GetText(span).ToString(), cancellationToken).ConfigureAwait(false);
                    }

                    Document.Remove(this, span);
                    break;
                }
            case (Control, C) when selection.TryGet(out var selectionValue):
                {
                    var copiedContent = Document.GetText(selectionValue).ToString();
                    await clipboard.SetTextAsync(copiedContent, cancellationToken).ConfigureAwait(false);
                    break;
                }
            case (Control | Shift, C):
                await clipboard.SetTextAsync(Document.GetText(), cancellationToken).ConfigureAwait(false);
                break;
            case (Shift, Insert) when key.PastedText is not null:
                PasteText(key.PastedText);
                break;
            case (Control, V):
            case (Control | Shift, V):
            case (Shift, Insert):
                var clipboardText = await clipboard.GetTextAsync(cancellationToken).ConfigureAwait(false);
                PasteText(clipboardText);
                break;
            case (Control, Z):
                Document.Undo(out var newSelection);
                Selection = newSelection;
                if (newSelection.HasValue)
                {
                    completionPane.IsOpen = false;
                    overloadPane.IsOpen = false;
                }
                break;
            case (Control, Y):
                Document.Redo(out newSelection);
                Selection = newSelection;
                if (newSelection.HasValue)
                {
                    completionPane.IsOpen = false;
                    overloadPane.IsOpen = false;
                }
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
        //Also replaces tabs as spaces and filters out special characters.
        string DedentMultipleLinesAndFilter(string text)
        {
            var sb = new StringBuilder();
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length > 1)
            {
                var nonEmptyLines = lines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                //we remove indentation only when there are multiple non-whitespace lines
                if (nonEmptyLines.Count <= 1)
                {
                    AppendFiltered(sb, text); // don't trim on purpose due to https://github.com/waf/PrettyPrompt/issues/168
                }
                else
                {
                    var leadingIndent = nonEmptyLines
                        .Select(line => line.TakeWhile(char.IsWhiteSpace).Count())
                        .Min();

                    if (leadingIndent == 0)
                    {
                        for (var i = 0; i < lines.Length; i++)
                        {
                            AppendFiltered(sb, lines[i]);
                            if (i != lines.Length - 1) sb.Append('\n');
                        }
                    }
                    else
                    {
                        //removing indentation
                        for (var i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i][Math.Min(lines[i].Length, leadingIndent)..];
                            AppendFiltered(sb, line);
                            if (i != lines.Length - 1) sb.Append('\n');
                        }
                    }
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

    internal void MeasureConsole()
    {
        var windowTopChange = console.WindowTop - this.windowTop;
        this.TopCoordinate = Math.Max(0, this.TopCoordinate - windowTopChange);
        this.windowTop = console.WindowTop;

        this.CodeAreaWidth = Math.Max(0, console.BufferWidth - configuration.Prompt.Length);
        this.CodeAreaHeight = Math.Max(0, console.WindowHeight - this.TopCoordinate);
    }

    public async Task OnKeyUp(KeyPress key, CancellationToken cancellationToken)
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

        await selectionHandler.OnKeyUp(key, cancellationToken).ConfigureAwait(false);

        CheckConsistency();
    }

    public ConsoleCoordinate GetHelperPanesStartPosition()
    {
        var filteredView = completionPane.FilteredView;

        int completionPaneWidth =
            filteredView.VisibleItems.Count > 0 ?
            BoxDrawing.GetHorizontalBordersWidth(BoxType.CompletionItems, configuration) + filteredView.VisibleItems.Max(w => UnicodeWidth.GetWidth(w.DisplayText)) :
            0;

        int overloadPaneWidth = overloadPane.Width;
        int documentationWidth = completionPane.SelectedItemDocumentationWidth;

        int requiredWidth = Math.Max(completionPaneWidth + documentationWidth - 1, overloadPaneWidth); //-1 because completionPane shares 1 column with documentationBox
        var codeAreaStartColumn = configuration.Prompt.Length;
        var cursor = Cursor;
        return new ConsoleCoordinate(
            row: cursor.Row + 1,
            column: requiredWidth > codeAreaWidth ? codeAreaStartColumn // not enough room to show to completion box. We'll position all the way to the left, and truncate the box.
                : cursor.Column + requiredWidth >= codeAreaWidth ? codeAreaWidth - requiredWidth // not enough room to show to completion box offset to the current cursor. We'll position it stuck to the right.
                : cursor.Column // enough room, we'll show the completion box offset at the cursor location.
        );
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