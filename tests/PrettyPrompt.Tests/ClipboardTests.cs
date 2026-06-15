using PrettyPrompt.TextSelection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PrettyPrompt.Tests;
public class ClipboardTests
{
    private readonly WrappedClipboard clipboard;

    public ClipboardTests()
    {
        this.clipboard = new WrappedClipboard();
    }

    [Fact]
    public async Task Clipboard_WrappedCopyPasting()
    {
        // This exercises the real OS clipboard (TextCopy). On Linux that needs an X selection - i.e. xsel and
        // a running display - which headless CI lacks. macOS (pbcopy/pbpaste) and Windows work without extra
        // setup, so only skip on displayless Linux; the Windows CI run still covers this happy path.
        Assert.SkipWhen(
            OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("DISPLAY") is null,
            "No X clipboard available (xsel requires a display) on headless Linux.");

        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            Assert.True(clipboard.TrySetText("hello"));
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.True(clipboard.TryGetText(out var pasted));
            Assert.Equal("hello", pasted);

            Assert.True(await clipboard.TrySetTextAsync("world", TestContext.Current.CancellationToken));
            await Task.Delay(100, TestContext.Current.CancellationToken);
            var (success, pastedAsync) = await clipboard.TryGetTextAsync(TestContext.Current.CancellationToken);
            Assert.True(success);
            Assert.Equal("world", pastedAsync);
        }
    }

    [Fact]
    public async Task Clipboard_WhenUnderlyingClipboardThrows_ReportsFailureWithoutThrowing()
    {
        // The OS clipboard can fail for reasons outside our control (missing xsel/clip.exe, no display,
        // the TextCopy helper process timing out - see https://github.com/waf/CSharpRepl/issues/327).
        // None of those should propagate out and crash the prompt; the Try* methods report failure instead,
        // which lets callers avoid destructive edits (e.g. not removing cut text that was never copied).
        var throwingClipboard = new WrappedClipboard(new ThrowingClipboard());

        Assert.False(throwingClipboard.TryGetText(out var text));
        Assert.Null(text);

        var (success, asyncText) = await throwingClipboard.TryGetTextAsync(TestContext.Current.CancellationToken);
        Assert.False(success);
        Assert.Null(asyncText);

        Assert.False(throwingClipboard.TrySetText("hello"));
        Assert.False(await throwingClipboard.TrySetTextAsync("hello", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Clipboard_WhenExecutableMissing_ThrowsHelpfulError()
    {
        // A missing clipboard tool (xsel/clip.exe) is an actionable setup problem rather than a transient
        // hiccup, so we surface it with a helpful message rather than silently swallowing it.
        var clipboard = new WrappedClipboard(new ThrowingClipboard("Could not execute process"));

        Assert.Contains("xsel", Assert.Throws<Exception>(() => clipboard.TryGetText(out _)).Message);
        Assert.Contains("xsel", (await Assert.ThrowsAsync<Exception>(async () => await clipboard.TryGetTextAsync(TestContext.Current.CancellationToken))).Message);
        Assert.Contains("xsel", Assert.Throws<Exception>(() => clipboard.TrySetText("hello")).Message);
        Assert.Contains("xsel", (await Assert.ThrowsAsync<Exception>(() => clipboard.TrySetTextAsync("hello", TestContext.Current.CancellationToken))).Message);
    }

    [Fact]
    public async Task Clipboard_WhenCancelled_PropagatesCancellation()
    {
        // Genuine cancellation should still surface - it isn't a clipboard failure to swallow.
        var clipboard = new WrappedClipboard(new ThrowingClipboard());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await clipboard.TryGetTextAsync(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clipboard.TrySetTextAsync("hello", cts.Token));
    }
}
