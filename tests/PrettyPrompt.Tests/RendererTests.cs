using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using PrettyPrompt.Consoles;
using PrettyPrompt.Panes;
using PrettyPrompt.TextSelection;
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
        // After "\n", Windows preserves the column (DISABLE_NEWLINE_AUTO_RETURN) and moves left to the next
        // line's content; other platforms return to column 1 (ONLCR) and move right to it.
        string renderedNewlineWithCursorReposition = " \n" + (OperatingSystem.IsWindows() ? AnsiEscapeCodes.GetMoveCursorLeft(24) : AnsiEscapeCodes.GetMoveCursorRight(2));
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
        // After "\n", Windows preserves the column (DISABLE_NEWLINE_AUTO_RETURN) and moves left to the next
        // line's content; other platforms return to column 1 (ONLCR) and move right to it.
        string renderedNewlineWithCursorReposition = " \n" + (OperatingSystem.IsWindows() ? AnsiEscapeCodes.GetMoveCursorLeft(24) : AnsiEscapeCodes.GetMoveCursorRight(2));
        string cursorRepositionToFirstLine = " \n" + AnsiEscapeCodes.GetMoveCursorUp(3) + (OperatingSystem.IsWindows() ? AnsiEscapeCodes.GetMoveCursorLeft(24) : AnsiEscapeCodes.GetMoveCursorRight(2));
        var expectedRender = string.Join(renderedNewlineWithCursorReposition, typedInput.Split('\n').Take(ConsoleHeight - 2)) + cursorRepositionToFirstLine;
        Assert.Equal(expectedRender, output);
    }

    [Fact]
    public void RenderPrompt_StartedAtBottomOfWindow_ReservesRoomForCompletionPane()
    {
        // prompt starts on the very bottom row of the window
        const int Height = 30;
        var console = ConsoleStub.NewConsole(width: 100, height: Height);
        console.CursorTop.Returns(Height - 1);
        console.WindowTop.Returns(0);
        var configuration = new PromptConfiguration();
        var renderer = new Renderer(console, configuration);
        var codePane = new CodePane(console, configuration, new PromptCallbacks(), Substitute.For<IClipboard>());

        var reserved = codePane.EmptySpaceAtBottomOfWindowHeight;

        renderer.RenderPrompt(codePane);

        // RenderPrompt writes `reserved` blank lines to make room for the completion pane; since the prompt
        // started at the bottom that scrolls the buffer up, moving the prompt up by `reserved` rows.
        Assert.Equal(Height - 1 - reserved, codePane.TopCoordinate);
        Assert.Equal(reserved + 1, codePane.CodeAreaHeight);
    }

    private (CodePane codePane, CompletionPane completionPane, OverloadPane overloadPane) BuildUIPanes(string typedInput)
    {
        var callbacks = Substitute.For<IPromptCallbacks>();
        var codePane = new CodePane(console, configuration, new PromptCallbacks(), new WrappedClipboard(Substitute.For<IClipboard>()));
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
