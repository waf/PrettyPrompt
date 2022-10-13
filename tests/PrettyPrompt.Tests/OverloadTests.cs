using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Tests;

public class OverloadTests
{
    [Fact]
    public async Task OverloadPane_WhenScrolled_NoException()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = CompletionTests.ConfigurePrompt(
            console,
            configuration: new PromptConfiguration(
                keyBindings: new KeyBindings(
                    commitCompletion: new[] { new KeyPressPattern(Tab) },
                    submitPrompt: new[] { new KeyPressPattern(Enter) },
                    newLine: new[] { new KeyPressPattern(Shift, Enter) },
                    triggerOverloadList: new(new KeyPressPattern('('))
                )
            ),
            completions: new[] { "ant" });
        console.StubInput(
            $"ant(", // should open overload list
            $"{DownArrow}{DownArrow}{UpArrow}", // navigate through overload list
            $"){Enter}"); //submit prompt
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ant()", result.Text);
    }
}
