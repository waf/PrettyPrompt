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
internal delegate Task<KeyPress> TransformKeyPressAsyncCallbackAsync(string text, int caret, KeyPress keyPress);
internal delegate Task<(IReadOnlyList<OverloadItem>, int ArgumentIndex)> GetOverloadsCallbackAsync(string text, int caret);

internal class TestPromptCallbacks : PromptCallbacks
{
    private readonly (KeyPressPattern, KeyPressCallbackAsync)[] keyPressCallbacks;

    public SpanToReplaceByCompletionCallbackAsync? SpanToReplaceByCompletionCallback { get; set; }
    public CompletionCallbackAsync? CompletionCallback { get; set; }
    public OpenCompletionWindowCallbackAsync? OpenCompletionWindowCallback { get; set; }
    public HighlightCallbackAsync? HighlightCallback { get; set; }
    public TransformKeyPressAsyncCallbackAsync? TransformKeyPressCallback { get; set; }
    public GetOverloadsCallbackAsync? GetOverloadsCallback { get; set; }

    public TestPromptCallbacks(params (KeyPressPattern Pattern, KeyPressCallbackAsync Callback)[]? keyPressCallbacks)
    {
        this.keyPressCallbacks = keyPressCallbacks ?? Array.Empty<(KeyPressPattern, KeyPressCallbackAsync)>();
    }

    protected override IEnumerable<(KeyPressPattern Pattern, KeyPressCallbackAsync Callback)> GetKeyPressCallbacks() => keyPressCallbacks;

    protected override Task<TextSpan> GetSpanToReplaceByCompletionAsync(string text, int caret, CancellationToken cancellationToken)
    {
        return
            SpanToReplaceByCompletionCallback is null ?
            base.GetSpanToReplaceByCompletionAsync(text, caret, cancellationToken) :
            SpanToReplaceByCompletionCallback(text, caret);
    }

    protected override Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
    {
        return
            CompletionCallback is null ?
            base.GetCompletionItemsAsync(text, caret, spanToBeReplaced, cancellationToken) :
            CompletionCallback(text, caret, spanToBeReplaced);
    }

    protected override Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret, KeyPress key, CancellationToken cancellationToken)
    {
        return
            OpenCompletionWindowCallback is null ?
            base.ShouldOpenCompletionWindowAsync(text, caret, key, cancellationToken) :
            OpenCompletionWindowCallback(text, caret);
    }

    protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
    {
        return
            HighlightCallback is null ?
            base.HighlightCallbackAsync(text, cancellationToken) :
            HighlightCallback(text);
    }

    protected override Task<KeyPress> TransformKeyPressAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        return
            TransformKeyPressCallback is null ?
            base.TransformKeyPressAsync(text, caret, keyPress, cancellationToken) :
            TransformKeyPressCallback(text, caret, keyPress);
    }

    protected override Task<(IReadOnlyList<OverloadItem>, int ArgumentIndex)> GetOverloadsAsync(string text, int caret, CancellationToken cancellationToken)
    {
        return
            GetOverloadsCallback is null ?
            base.GetOverloadsAsync(text, caret, cancellationToken) :
            GetOverloadsCallback(text, caret);
    }
}