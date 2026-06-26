#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Panes;
using PrettyPrompt.TextSelection;

/// <summary>
/// Honest steady-state per-keystroke cost in a large document. Unlike <see cref="PerKeystrokeBenchmark"/>
/// (which measures the hot stages in isolation against a blank screen), this drives the real per-keystroke
/// path: it holds a seeded <see cref="CodePane"/> and a persistent <see cref="Renderer"/> whose previous
/// screen is kept across invocations, so each measured keystroke pays the realistic cost - re-wrap (via the
/// real Document.Changed event), the GetText O(n) copy, MeasureConsole, cell pooling (warm), and the
/// incremental ANSI diff against the near-identical previous screen (so the diff/write is cheap, as in
/// production). This is the baseline to measure the Tier A (allocation) and Tier E (screen-buffer pooling)
/// work against, and it captures the loop costs the isolated microbenchmarks miss.
///
/// Each op is a single keystroke. State is kept net-neutral across invocations by toggling: Navigate moves
/// the caret one cluster right/left around the document midpoint; Type alternates inserting and deleting one
/// character there. The user's syntax-highlight callback cost is deliberately excluded (it's application
/// code, not library code) - a precomputed span list is returned so we still exercise the highlight cache
/// compare and the highlight-to-cell mapping without an O(n) re-scan per keystroke.
///
/// Run with: dotnet run -c Release --project tests/PrettyPrompt.Benchmarks -- --filter *SteadyState*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class SteadyStateRenderBenchmark
{
    private const int ConsoleWidth = 120;
    private const int ConsoleHeight = 80;

    [Params(10, 100, 1000)]
    public int DocumentLineCount;

    private Renderer renderer = null!;
    private CodePane codePane = null!;
    private OverloadPane overloadPane = null!;
    private CompletionPane completionPane = null!;
    private SyntaxHighlighter highlighter = null!;
    private KeyPress key = null!;
    private int caretMid;
    private bool toggled;

    [GlobalSetup]
    public void Setup()
    {
        var text = GenerateDocument(DocumentLineCount);
        var highlights = GenerateHighlights(text);

        var console = new SilentConsole(ConsoleWidth, ConsoleHeight);
        var configuration = new PromptConfiguration();
        var callbacks = new PrecomputedHighlightCallbacks(highlights);

        renderer = new Renderer(console, configuration);
        codePane = new CodePane(console, configuration, callbacks, new WrappedClipboard());
        overloadPane = new OverloadPane(codePane, callbacks, configuration);
        completionPane = new CompletionPane(codePane, overloadPane, callbacks, configuration);
        codePane.Bind(completionPane, overloadPane);
        highlighter = new SyntaxHighlighter(callbacks, hasUserOptedOutFromColor: false);

        // seed the document and park the caret in the middle (the realistic editing position)
        codePane.MeasureConsole();
        codePane.Document.InsertAtCaret(codePane, text);
        caretMid = text.Length / 2;
        codePane.Document.Caret = caretMid;

        key = new KeyPress(new ConsoleKeyInfo('x', ConsoleKey.X, shift: false, alt: false, control: false));

        // establish the previous screen so subsequent renders exercise the incremental diff, not a full redraw
        RenderAfterChange();
    }

    /// <summary>One arrow keystroke: caret-only move (no text change) + render. Today this still triggers a full re-wrap.</summary>
    [Benchmark(Description = "Navigate (caret move + render)")]
    public void Navigate()
    {
        codePane.MeasureConsole();
        toggled = !toggled;
        codePane.Document.Caret = toggled ? caretMid + 1 : caretMid;
        RenderAfterChange();
    }

    /// <summary>One editing keystroke: insert or delete a character at the midpoint + render. Alternates so the document stays net-neutral.</summary>
    [Benchmark(Description = "Type (insert/delete + render)")]
    public void Type()
    {
        codePane.MeasureConsole();
        if (toggled)
        {
            codePane.Document.Remove(codePane, codePane.Document.Caret - 1, 1);
        }
        else
        {
            codePane.Document.InsertAtCaret(codePane, 'x');
        }
        toggled = !toggled;
        RenderAfterChange();
    }

    private void RenderAfterChange()
    {
        // mirrors the per-keystroke tail of Prompt.ReadLineAsync's render path
        codePane.MeasureConsole();
        var text = codePane.Document.GetText();
        var highlights = highlighter.HighlightAsync(text, CancellationToken.None).GetAwaiter().GetResult();
        renderer.RenderOutput(result: null, codePane, overloadPane, completionPane, highlights, key);
    }

    private static readonly string[] Words =
    {
        "apple", "the", "banana", "quick", "mango", "brown", "grape", "fox",
        "melon", "jumps", "orange", "over", "pear", "lazy", "peach", "dog",
    };

    private static readonly string[] HighlightedWords =
    {
        "apple", "banana", "mango", "grape", "melon", "orange", "pear", "peach",
    };

    private static string GenerateDocument(int lineCount)
    {
        var sb = new StringBuilder();
        int w = 0;
        for (int line = 0; line < lineCount; line++)
        {
            int col = 0;
            // ~160-column lines so each wraps at the console width
            while (col < 160)
            {
                if (col > 0) { sb.Append(' '); col++; }
                var word = Words[w++ % Words.Length];
                sb.Append(word);
                col += word.Length;
            }
            if (line < lineCount - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static IReadOnlyCollection<FormatSpan> GenerateHighlights(string text)
    {
        var spans = new List<FormatSpan>();
        foreach (var word in HighlightedWords)
        {
            int idx = 0;
            while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                spans.Add(new FormatSpan(idx, word.Length, AnsiColor.Green));
                idx += word.Length;
            }
        }
        return spans;
    }

    /// <summary>Returns the same precomputed spans regardless of input, so the highlight callback is O(1) and we measure library cost, not user-callback cost.</summary>
    private sealed class PrecomputedHighlightCallbacks : PromptCallbacks
    {
        private readonly IReadOnlyCollection<FormatSpan> highlights;
        public PrecomputedHighlightCallbacks(IReadOnlyCollection<FormatSpan> highlights) => this.highlights = highlights;

        protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
            => Task.FromResult(highlights);
    }

    /// <summary>A no-op console with a fixed size; output is discarded so we measure work, not terminal I/O.</summary>
    private sealed class SilentConsole : IConsole
    {
        public SilentConsole(int width, int height)
        {
            BufferWidth = width;
            WindowHeight = height;
        }

        public int CursorTop => 0;
        public int BufferWidth { get; }
        public int WindowHeight { get; }
        public int WindowTop => 0;
        public bool KeyAvailable => false;
        public bool IsErrorRedirected => false;
        public bool CaptureControlC { get => false; set { } }

        public event ConsoleCancelEventHandler? CancelKeyPress { add { } remove { } }

        public ConsoleKeyInfo ReadKey(bool intercept) => default;
        public void Clear() { }
        public void HideCursor() { }
        public void ShowCursor() { }
        public void InitVirtualTerminalProcessing() { }
        public void Write(string? value) { }
        public void WriteError(string? value) { }
        public void WriteErrorLine(string? value) { }
        public void WriteLine(string? value) { }
        public void Write(ReadOnlySpan<char> value) { }
        public void WriteError(ReadOnlySpan<char> value) { }
        public void WriteErrorLine(ReadOnlySpan<char> value) { }
        public void WriteLine(ReadOnlySpan<char> value) { }
    }
}
