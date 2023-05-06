using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using PrettyPrompt.Consoles;
using PrettyPrompt.Panes;
using TextCopy;
using Xunit;

namespace PrettyPrompt.Tests;

public class RendererTests
{
    private const int ConsoleHeight = 5;
    private readonly IConsole console;
    private readonly PromptConfiguration configuration;
    private readonly Renderer renderer;

    public RendererTests()
    {
        this.console = ConsoleStub.NewConsole(width: 100, height: ConsoleHeight);
        this.configuration = new PromptConfiguration();
        this.renderer = new Renderer(console, configuration);
    }

    [Fact]
    public void RenderOutput_ConsoleHeightTooSmall_ShowsTrailingLinesThatFitInViewport()
    {
        var typedInput = """
            Console.WriteLine("A");
            Console.WriteLine("B");
            Console.WriteLine("C");
            Console.WriteLine("D");
            Console.WriteLine("E");
            """.Replace("\r\n", "\n");

        var (codePane, completionPane, overloadPane) = BuildUIPanes(typedInput);

        // system under test
        renderer.RenderOutput(
            result: null,
            codePane,
            overloadPane,
            completionPane,
            Array.Empty<Highlighting.FormatSpan>(),
            new KeyPress(new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false))
        );

        var output = GetRenderedOutput(console);

        // because the console height is 5, with 2 lines of padding, and the cursor is on the final line,
        // we should only render the last 3 lines.  there will be some ansi escape sequences for newlines here as well.
        const string renderedNewlineWithCursorReposition = " \n[24D";
        var expectedRender = string.Join(renderedNewlineWithCursorReposition, typedInput.Split('\n').TakeLast(ConsoleHeight - 2));
        Assert.Equal(expectedRender, output);
    }

    [Fact]
    public async Task RenderOutput_ConsoleHeightTooSmallAndCursorOnFirstLine_ShowsInitialLinesThatFitInViewport()
    {
        var typedInput = """
            Console.WriteLine("A");
            Console.WriteLine("B");
            Console.WriteLine("C");
            Console.WriteLine("D");
            Console.WriteLine("E");
            """.Replace("\r\n", "\n");

        var (codePane, completionPane, overloadPane) = BuildUIPanes(typedInput);
        // navigate to first line
        await codePane.OnKeyDown(new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, true)), CancellationToken.None);

        // system under test
        renderer.RenderOutput(
            result: null,
            codePane,
            overloadPane,
            completionPane,
            Array.Empty<Highlighting.FormatSpan>(),
            new KeyPress(new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false))
        );

        var output = GetRenderedOutput(console);

        // because the console height is 5, with 2 lines of padding, with the cursor on the first line,
        // we should only render the first 3 lines. There will be some ansi escape sequences for newlines here as well.
        const string renderedNewlineWithCursorReposition = " \n[24D";
        const string cursorRepositionToFirstLine = " \n[3A[24D";
        var expectedRender = string.Join(renderedNewlineWithCursorReposition, typedInput.Split('\n').Take(ConsoleHeight - 2)) + cursorRepositionToFirstLine;
        Assert.Equal(expectedRender, output);
    }

    private (CodePane codePane, CompletionPane completionPane, OverloadPane overloadPane) BuildUIPanes(string typedInput)
    {
        var callbacks = Substitute.For<IPromptCallbacks>();
        var codePane = new CodePane(console, configuration, Substitute.For<IClipboard>());
        codePane.Document.InsertAtCaret(codePane, typedInput);
        var overloadPane = new OverloadPane(codePane, callbacks, configuration)
        {
            IsOpen = false
        };
        var completionPane = new CompletionPane(codePane, overloadPane, callbacks, configuration);
        codePane.Bind(completionPane, overloadPane);
        return (codePane, completionPane, overloadPane);
    }

    private static string? GetRenderedOutput(IConsole console)
    {
        var write = console.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(Console.Write)).Last();
        var output = write.GetArguments()?.SingleOrDefault()?.ToString();
        return output;
    }
}
