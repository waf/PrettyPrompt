#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Rendering;
using PrettyPrompt.TextSelection;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Panes;

internal class CodePane : IKeyPressHandler
{
    private readonly IConsole console;
    private readonly PromptConfiguration configuration;
    private readonly IPromptCallbacks promptCallbacks;
    private readonly WrappedClipboard clipboard;
    private readonly SelectionKeyPressHandler selectionHandler;
    private int topCoordinate;
    private int codeAreaWidth;
    private int codeAreaHeight;
    private WordWrappedText wordWrappedText;
    private int lastWordWrapWidth;
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
            Debug.Assert(value <= console.WindowHeight);
            topCoordinate = value;
        }
    }

    public int CodeAreaWidth
    {
        get => codeAreaWidth;
        private set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= console.BufferWidth);
            codeAreaWidth = value;
        }
    }

    public int CodeAreaHeight
    {
        get => codeAreaHeight;
        private set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= console.WindowHeight);
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

    public CodePane(IConsole console, PromptConfiguration configuration, IPromptCallbacks promptCallbacks, WrappedClipboard clipboard)
    {
        this.console = console;
        this.configuration = configuration;
        this.promptCallbacks = promptCallbacks;
        this.clipboard = clipboard;

        TopCoordinate = console.CursorTop;
        MeasureConsole();

        Document = new Document();
        Document.TextChanged += WordWrap;   // full re-wrap only when the text actually changes
        Document.Changed += SyncCursor;     // every change (incl. caret-only moves): keep the 2-D cursor in sync
        selectionHandler = new SelectionKeyPressHandler(this);
        TabSpaces = new string(' ', configuration.TabSize);

        WordWrap();

        void WordWrap()
        {
            wordWrappedText = Document.WrapEditableCharacters(CodeAreaWidth);
            lastWordWrapWidth = CodeAreaWidth;
        }

        // On a caret-only move (arrow keys, Home/End, etc.) the text is unchanged, so we skip the O(n) re-wrap
        // and recompute just the 2-D cursor from the existing wrapped lines (see PERFORMANCE_PLAN.md Tier B1).
        // On a text edit, WordWrap (subscribed to TextChanged, which StringBuilderWithCaret fires *before*
        // Changed) has already re-wrapped and set the cursor, so this is a cheap redundant no-op. If the
        // code-area width changed (terminal resize) the existing wrapped lines are stale, so fall back to a full
        // re-wrap - matching the pre-Tier-B1 behavior where every Document change re-wrapped at the current width.
        void SyncCursor()
        {
            if (CodeAreaWidth != lastWordWrapWidth)
            {
                WordWrap();
                return;
            }

            var recomputed = wordWrappedText.GetCursorForCaret(Document.Caret);
#if DEBUG
            var fromFullWrap = Document.WrapEditableCharacters(CodeAreaWidth).Cursor;
            Debug.Assert(recomputed.Equals(fromFullWrap), $"Caret-only cursor recompute ({recomputed}) disagrees with a full wrap ({fromFullWrap}) at caret {Document.Caret}.");
#endif
            wordWrappedText.Cursor = recomputed;
        }
    }

    internal void Bind(CompletionPane completionPane, OverloadPane overloadPane)
    {
        this.completionPane = completionPane;
        this.overloadPane = overloadPane;
    }

    /// <summary>
    /// Re-capture the submit result's text from the current document, so the returned result matches
    /// what's shown (and saved to history) after auto-formatting, not the pre-format snapshot.
    /// </summary>
    internal void RefreshSubmitResultText()
    {
        if (Result is not { IsSuccess: true })
        {
            return;
        }
        Result = new PromptResult(isSuccess: true, Document.GetText().EnvironmentNewlines(), Result.SubmitKeyInfo);
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
            case (Shift, LeftArrow) or LeftArrow or (Control, B): // Ctrl+B = backward one char (emacs)
                Document.Caret = Document.CalculateCaretIndexToLeft();
                break;
            case (Shift, RightArrow) or RightArrow or (Control, F): // Ctrl+F = forward one char (emacs)
                Document.Caret = Document.CalculateCaretIndexToRight();
                break;
            case (Control | Shift, LeftArrow) or (Control, LeftArrow) or (Alt, B): // Alt+b = backward one word (emacs)
                Document.MoveToWordBoundary(-1);
                break;
            case (Control | Shift, RightArrow) or (Control, RightArrow) or (Alt, F): // Alt+f = forward one word (emacs)
                Document.MoveToWordBoundary(+1);
                break;
            case (Control, Backspace)
                or (Control, H)       // Ctrl+H = delete word backward, an alias of Ctrl+Backspace. Ideally this would be 'delete one character' but on Unix/macOS .NET reports the Ctrl+H byte (0x08) as (Control, Backspace). https://github.com/dotnet/runtime/issues/73379#issuecomment-1206168139
                or (Alt, Backspace)   // Alt+Backspace = delete word backward (emacs)
                when selection is null:
                {
                    var startDeleteIndex = Document.CalculateWordBoundaryIndexNearCaret(-1);
                    Document.Remove(this, startDeleteIndex, Document.Caret - startDeleteIndex);
                    break;
                }
            case (Control, Delete)
                or (Alt, Delete)      // Alt+Delete = delete word forward (Mac Option+forward-delete)
                or (Alt, D)           // Alt+d = delete word forward (emacs)
                when selection is null:
                {
                    var endDeleteIndex = Document.CalculateWordBoundaryIndexNearCaret(+1);
                    Document.Remove(this, Document.Caret, endDeleteIndex - Document.Caret);
                    break;
                }
            case Backspace when selection is null:
                {
                    // delete the whole grapheme cluster to the left, not a single UTF-16 code unit
                    var clusterStart = Document.CalculateCaretIndexToLeft();
                    Document.Remove(this, clusterStart, Document.Caret - clusterStart);
                    break;
                }
            case (Delete or (Control, D)) when selection is null: // Ctrl+D = delete one char forwards (emacs)
                {
                    // delete the whole grapheme cluster to the right, not a single UTF-16 code unit
                    var clusterEnd = Document.CalculateCaretIndexToRight();
                    Document.Remove(this, Document.Caret, clusterEnd - Document.Caret);
                    break;
                }
            case (_, Delete) or (_, Backspace) or Delete or Backspace or (Control, D) or (Alt, D) or (Control, H) or (Control, K) or (Control, U) when selection.HasValue:
                Document.DeleteSelectedText(this);
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
                    if (await clipboard.TrySetTextAsync(cutContent, cancellationToken).ConfigureAwait(false))
                    {
                        Selection = null;
                        Document.Remove(this, selectionValue);
                    }
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
                        if (await clipboard.TrySetTextAsync(Document.GetText(span).ToString(), cancellationToken).ConfigureAwait(false))
                        {
                            Document.Remove(this, span);
                        }
                    }
                    else
                    {
                        // Shift+Delete just deletes the line; it never touches the clipboard.
                        Document.Remove(this, span);
                    }
                    break;
                }
            case (Control, K) when selection is null: // Ctrl+K = delete from caret to end of the current line (emacs/readline kill-line); deletes only, does not write the clipboard
                {
                    var lineEnd = Document.CalculateLineBoundaryIndexNearCaret(+1, smartHome: false);
                    var span = TextSpan.FromBounds(Document.Caret, lineEnd);
                    Document.Remove(this, span);
                    break;
                }
            case (Control, U) when selection is null: // Ctrl+U = delete from start of the current line to caret (readline/bash unix-line-discard; in emacs Ctrl+U is universal-argument); deletes only, does not write the clipboard
                {
                    var lineStart = Document.CalculateLineBoundaryIndexNearCaret(-1, smartHome: false);
                    var span = TextSpan.FromBounds(lineStart, Document.Caret);
                    Document.Remove(this, span);
                    break;
                }
            case (Control, C) when selection.TryGet(out var selectionValue):
                {
                    var copiedContent = Document.GetText(selectionValue).ToString();
                    // copy doesn't mutate the document, so a failed clipboard write is simply a no-op
                    _ = await clipboard.TrySetTextAsync(copiedContent, cancellationToken).ConfigureAwait(false);
                    break;
                }
            case (Control | Shift, C):
                _ = await clipboard.TrySetTextAsync(Document.GetText(), cancellationToken).ConfigureAwait(false);
                break;
            case (Shift, Insert) when key.PastedText is not null:
                PasteText(key.PastedText);
                break;
            case (Control, V) or (Control | Shift, V) or (Shift, Insert):
                {
                    var (success, clipboardText) = await clipboard.TryGetTextAsync(cancellationToken).ConfigureAwait(false);
                    if (success)
                    {
                        PasteText(clipboardText);
                    }
                    break;
                }
            case (Control, Z):
                {
                    Document.Undo(out var undoSelection);
                    Selection = undoSelection;
                    if (undoSelection.HasValue)
                    {
                        completionPane.IsOpen = false;
                        overloadPane.IsOpen = false;
                    }
                    break;
                }
            case (Control, Y):
                {
                    Document.Redo(out var redoSelection);
                    Selection = redoSelection;
                    if (redoSelection.HasValue)
                    {
                        completionPane.IsOpen = false;
                        overloadPane.IsOpen = false;
                    }
                    break;
                }
            default:
                {
                    if (!(char.IsControl(key.ConsoleKeyInfo.KeyChar) || promptCallbacks.TryGetKeyPressCallbacks(key.ConsoleKeyInfo, out _)))
                    {
                        Document.InsertAtCaret(this, key.ConsoleKeyInfo.KeyChar);
                    }
                    break;
                }
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
                        .Min(line => line.TakeWhile(char.IsWhiteSpace).Count());

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
                else if (c == '\n')
                {
                    sb.Append(c); // preserve newlines (multi-line paste)
                }
                else if (char.IsControl(c))
                {
                    // Strip other control characters (e.g. \r, NUL, ESC) that would corrupt terminal
                    // rendering. We must NOT filter by display width here: zero-width scalars such as
                    // combining marks, zero-width joiners, and variation selectors are legitimate parts
                    // of grapheme clusters (e.g. the emoji sequence 🤦🏼‍♂️), and dropping them breaks the
                    // cluster apart. See issue #270.
                    continue;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
    }

    /// <summary>
    /// Corrects TopCoordinate after RenderPrompt writes blank lines to reserve completion-pane room.
    /// - near the window bottom, those lines scroll the buffer up, so the prompt moves up by however many didn't fit below the cursor.
    /// - Windows self-corrects via MeasureConsole each keystroke; Linux/macOS can't, so we adjust here from what we wrote (no cursor read).
    /// - without it, CodeAreaHeight collapses to ~1 and the completion pane can't draw until the next submission. https://github.com/waf/CSharpRepl/issues/395
    /// </summary>
    internal void AdjustTopCoordinateForReservedLines(int reservedLines)
    {
        var rowsBelowCursor = console.WindowHeight - 1 - TopCoordinate;
        var scrolledRows = Math.Max(0, reservedLines - rowsBelowCursor);
        if (scrolledRows > 0)
        {
            TopCoordinate = Math.Max(0, TopCoordinate - scrolledRows);
            CodeAreaHeight = Math.Max(0, console.WindowHeight - TopCoordinate);
        }
    }

    internal void MeasureConsole()
    {
        // Re-derive TopCoordinate from the live cursor only on Windows (CursorTop is a cheap local API there); see PR #231 for why it's needed.
        // - kept Windows-only on purpose: on Linux/macOS CursorTop blocks on an ESC[6n round-trip per read (a network round-trip over SSH), and this runs twice per keystroke.
        // - dotnet/runtime#88343 (input corruption from that read) is now fixed, but the round-trip cost remains, so we still avoid it off-Windows.
        if(OperatingSystem.IsWindows())
        {
            TopCoordinate = Math.Max(0, console.CursorTop - console.WindowTop - Cursor.Row);
        }
        this.CodeAreaWidth = Math.Max(0, console.BufferWidth - configuration.Prompt.Length);
        this.CodeAreaHeight = Math.Max(0, console.WindowHeight - this.TopCoordinate);
        Debug.WriteLine($"CodeAreaHeight: {CodeAreaHeight} TopCoordinate: {TopCoordinate}");
    }

    public async Task OnKeyUp(KeyPress key, CancellationToken cancellationToken)
    {
        if (key.Handled) return;

        switch (key.ObjectPattern)
        {
            case (Shift, UpArrow) or UpArrow or (Control, P) when Cursor.Row > 0: // Ctrl+P = previous line (emacs)
                {
                    var newCursor = Cursor.MoveUp();
                    var aboveLine = WordWrappedLines[newCursor.Row];
                    Cursor = newCursor.WithColumn(Math.Min(aboveLine.Content.AsSpan().TrimEnd().Length, newCursor.Column));
                    Document.SetCaretRoundedToGraphemeBoundary(aboveLine.StartIndex + Cursor.Column);
                    key.Handled = true;
                    break;
                }
            case (Shift, DownArrow) or DownArrow or (Control, N) when Cursor.Row < WordWrappedLines.Count - 1: // Ctrl+N = next line (emacs)
                {
                    var newCursor = Cursor.MoveDown();
                    var belowLine = WordWrappedLines[newCursor.Row];
                    Cursor = newCursor.WithColumn(Math.Min(belowLine.Content.AsSpan().TrimEnd().Length, newCursor.Column));
                    Document.SetCaretRoundedToGraphemeBoundary(belowLine.StartIndex + Cursor.Column);
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
            row: Math.Min(cursor.Row, Math.Max(CodeAreaHeight - EmptySpaceAtBottomOfWindowHeight - 1, 0)) + 1,
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