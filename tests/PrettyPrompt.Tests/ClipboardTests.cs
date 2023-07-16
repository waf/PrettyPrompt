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
        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            clipboard.SetText("hello");
            await Task.Delay(100);
            var pasted = clipboard.GetText();
            Assert.Equal("hello", pasted);

            await clipboard.SetTextAsync("world");
            await Task.Delay(100);
            pasted = await clipboard.GetTextAsync();
            Assert.Equal("world", pasted);
        }
    }

}
