# Architecture

This document explains how PrettyPrompt works under the hood, and how to get around the codebase. If you just want to use PrettyPrompt as a library, you don't need to read this document!

## Running PrettyPrompt

1. Main library entry point is `src/PrettyPrompt/Prompt.cs`.
1. The Main method of the example application is `examples/PrettyPrompt.Examples.FruitPrompt/Program.cs`.

## Notes

The basic architecture of PrettyPrompt is:

- Prompt.cs - configures the terminal and is the root entry point of the library. Starts the main KeyPress loop for reading console input. We intercept key presses, and allow the below `IKeyPressHandler` implementions to process them, so we can print the resulting character in the correct syntax highlighted color.
- Panes - UI components. They implement `IKeyPressHandler` to react to key presses.
    - CodePane - represents the rectangular area that the code is being typed into. Backing store is StringBuilder object, with an integer Caret property that represents the index of the caret in the StringBuilder. It also contains a Cursor point that represents the two dimensional row, column of the Caret, which is important when the text is word-wrapped.
    - CompletionPane - The intellisense window. The backing datastructure is a SlidingArrayWindow. The "sliding" represents the scrolling of the window.
- Each pane has a chance to process KeyDown events. After all KeyDown handlers run, the resulting text is word wrapped, and then all the KeyUp handlers fire.
- Rendering:
    - Code is syntax highlighted according to the callback provided by the user.
    - The models (CodePane and CompletionPane) are converted into a grid of `Cell` objects. Each cell is a single character on screen.
    - The incremental renderer compares the new grid of cells with the current grid of cells on screen. It determines the minimal ANSI Escape Sequence string, including cursor movement commands, to emit in order to update the screen. This string is written in a single Console.Write call, so one write can update all parts of the screen.


**C# Console APIs vs ANSI Escape Sequences**

The typical System.Console APIs control input, output, and screen navigation / manipulation (e.g. setting cursor position, clearing the screen, etc.). These trace their origins back to when .NET Framework ran on Windows, but the APIs are now mostly cross-platform. Some operations still throw NotImplementExceptions on Linux / Mac OS.

Linux and Mac OS use ANSI Escape Sequences for control, instead. [Windows recently added support for them, too](https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences), making them a good cross-platform alternative.  On Windows, ANSI Escape Sequences for output can be enabled with `SetConsoleMode` and `ENABLE_VIRTUAL_TERMINAL_PROCESSING`. For input, it can be enabled using `ENABLE_VIRTUAL_TERMINAL_INPUT`.

Since PrettyPrompt is cross-platform, we need to decide between the above two approaches. I settled on the following:

- Use ANSI Escape Sequences for output - syntax highlighting colors, moving around the screen (e.g. setting cursor coordinates), and clearing the screen. See `AnsiEscapeCodes.cs`.
- Use Console APIs for input - Reading characters from STDIN. See `SystemConsole.cs`.

For output, ANSI Escape Sequences have the benefit of being in-band signaling, so they're threadsafe, as opposed to the static global Console APIs like `Console.ForegroundColor`. Additionally, by treating these sequences as data, it makes it straighforward to incrementally update the screen by emitting streams of characters, some of which are ANSI escape sequences.

For input, it's less clear-cut. It came down to a few tradeoffs:

1. A drawback of input ANSI escape sequences is we can't detect the <kbd>Shift</kbd> modifier (used for <kbd>Shift + Enter</kbd> for inserting soft newlines).
2. Another drawback is that they're more cryptic, and we'd need to implement ANSI input escape sequence parsing ourselves. There are plenty of ANSI *output* libraries, mostly focused on screen colors, but no *input* libraries.
3. A benefit of input ANSI escape sequences is that we can get access to Bracketed Paste (i.e. special characters that wrap pasted input). This is important for performance because if the user pastes a large input into the prompt, we would normally see it as a stream of key presses, and wouldn't want to spend CPU on syntax highlighting and code completion on each keypress.

I managed to find a workaround for item #3, by using the `Console.KeyAvailable` property to detect a "full buffer" of incoming keys, and batch them into a single paste event. With this workaround in place, the drawbacks of ANSI escape sequence input outweighed the benefits.