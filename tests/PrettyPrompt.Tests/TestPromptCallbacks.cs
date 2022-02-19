using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Tests;

internal delegate Task<TextSpan> SpanToReplaceByCompletionCallbackAsync(string text, int caret);
internal delegate Task<IReadOnlyList<CompletionItem>> CompletionCallbackAsync(string text, int caret, TextSpan spanToBeReplaced);
internal delegate Task<bool> OpenCompletionWindowCallbackAsync(string text, int caret);
internal delegate Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text);
internal delegate Task<bool> InterpretKeyPressAsInputSubmitCallbackAsync(string text, int caret, ConsoleKeyInfo keyInfo);

internal class TestPromptCallbacks : PromptCallbacks
{
    private readonly (KeyPressPattern, KeyPressCallbackAsync)[] keyPressCallbacks;

    public SpanToReplaceByCompletionCallbackAsync? SpanToReplaceByCompletionCallback { get; set; }
    public CompletionCallbackAsync? CompletionCallback { get; set; }
    public OpenCompletionWindowCallbackAsync? OpenCompletionWindowCallback { get; set; }
    public HighlightCallbackAsync? HighlightCallback { get; set; }
    public InterpretKeyPressAsInputSubmitCallbackAsync? InterpretKeyPressAsInputSubmitCallback { get; set; }

    public TestPromptCallbacks(params (KeyPressPattern Pattern, KeyPressCallbackAsync Callback)[]? keyPressCallbacks)
    {
        this.keyPressCallbacks = keyPressCallbacks ?? Array.Empty<(KeyPressPattern, KeyPressCallbackAsync)>();
    }

    protected override IEnumerable<(KeyPressPattern Pattern, KeyPressCallbackAsync Callback)> GetKeyPressCallbacks() => keyPressCallbacks;

    protected override Task<TextSpan> GetSpanToReplaceByCompletionkAsync(string text, int caret, CancellationToken cancellationToken)
    {
        return
            SpanToReplaceByCompletionCallback is null ?
            base.GetSpanToReplaceByCompletionkAsync(text, caret, cancellationToken) :
            SpanToReplaceByCompletionCallback(text, caret);
    }

    protected override Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
    {
        return
            CompletionCallback is null ?
            base.GetCompletionItemsAsync(text, caret, spanToBeReplaced, cancellationToken) :
            CompletionCallback(text, caret, spanToBeReplaced);
    }

    protected override Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret, CancellationToken cancellationToken)
    {
        return
            OpenCompletionWindowCallback is null ?
            base.ShouldOpenCompletionWindowAsync(text, caret, cancellationToken) :
            OpenCompletionWindowCallback(text, caret);
    }

    protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
    {
        return
            HighlightCallback is null ?
            base.HighlightCallbackAsync(text, cancellationToken) :
            HighlightCallback(text);
    }

    protected override Task<bool> InterpretKeyPressAsInputSubmitAsync(string text, int caret, ConsoleKeyInfo keyInfo, CancellationToken cancellationToken)
    {
        return
            InterpretKeyPressAsInputSubmitCallback is null ?
            base.InterpretKeyPressAsInputSubmitAsync(text, caret, keyInfo, cancellationToken) :
            InterpretKeyPressAsInputSubmitCallback(text, caret, keyInfo);
    }
}