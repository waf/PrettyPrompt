# PrettyPrompt

A command line prompt that features syntax highlighting, autocompletion, history and more! `Console.ReadLine()` on steroids.

## Features

- Syntax highlighting support via ANSI escape sequences.
- Autocompletion ("intellisense") menu
- History
- Optionally detects "incomplete" lines and converts "hard newlines" (<kbd>Enter</kbd>) to "soft enters" (<kbd>Shift-Enter</kbd>) 
- Soft newlines (Shift-Enter) for multi-lien input
- Word wrapping
- Familiar keybindings (Home, End, arrow keys, Ctrl-L to clear screen, Ctrl-C to cancel current line, etc)
- Works "in-line" on the command line; it doesn't take over the entire terminal window.

## Usage

A simple "hello world" looks like this:

```csharp

var prompt = new Prompt();

while (true)
{
    var response = await prompt.ReadLineAsync("> ");
    if (response.Success) // false if user cancels, i.e. ctrl-c
    {
        if (response.Text == "exit") break;

        Console.WriteLine("You wrote " + response.Text);
    }
}
```

The `Prompt` constructor takes optional configuration options for enabling syntax highlighting, autocompletion.

For a more complete example that demonstrates syntax highlighting, autocompletion, and more, see the project in the `examples` directory.
If you have the [`dotnet example`](https://github.com/patriksvensson/dotnet-example) global tool installed, Run `dotnet example FruitPrompt`
in the repository root to start the example.


Screenshot:

![Screenshot](images/screenshot.png)
