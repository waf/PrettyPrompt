using PrettyPrompt.TextSelection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            clipboard.SetText("hello");
            await Task.Delay(100, TestContext.Current.CancellationToken);
            var pasted = clipboard.GetText();
            Assert.Equal("hello", pasted);

            await clipboard.SetTextAsync("world", TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            pasted = await clipboard.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.Equal("world", pasted);
        }
    }

}
