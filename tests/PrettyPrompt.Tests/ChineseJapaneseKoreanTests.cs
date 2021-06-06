using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;

namespace PrettyPrompt.Tests
{
    /// <summary>
    /// CJK Tests. Right now, only the "C" is tested, but I believe that all CJK renders the same way to the terminal.
    /// The key issues is that CJK characters can be the width of two characters when rendered to the terminal.
    /// </summary>
    public class ChineseJapaneseKoreanTests
    {
        [Fact]
        public async Task ReadLineAsync_EnteredCJKText_ReturnsText()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"书桌上有一个苹果。{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync("> ");

            Assert.Equal("书桌上有一个苹果。", result.Text);
        }

        [Fact]
        public async Task ReadLineAsync_NavigateCJKText_Navigates()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"世界{LeftArrow}{LeftArrow}你好，{Enter}");

            var prompt = new Prompt(console: console);
            var result = await prompt.ReadLineAsync("> ");

            Assert.Equal("你好，世界", result.Text);
        }

        [Fact]
        public async Task ReadLineAsync_TypesCJKText_IncrementalRenders()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"书桌上有{Enter}");

            var prompt = new Prompt(console: console);

            var result = await prompt.ReadLineAsync("> ");
            Assert.Equal("书桌上有", result.Text);

            var output = console.GetAllOutput();

            // we should render character by character as we're typing, with no extra rerenders happening on each keypress
            Assert.EndsWith("> ", output[0]);
            Assert.Equal("书", output[1]);
            Assert.Equal("桌", output[2]);
            Assert.Equal("上", output[3]);
            Assert.Equal("有", output[4]);
        }

        [Fact]
        public async Task ReadLineAsync_CompletesCJKText_Completes()
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"书{Enter}{Enter}");

            var prompt = new Prompt(console: console, callbacks: new PromptCallbacks
            {
                CompletionCallback = new CompletionTestData(new[] { "书桌上有" }).CompletionHandlerAsync
            });
            var result = await prompt.ReadLineAsync("> ");

            Assert.Equal("书桌上有", result.Text);
        }
    }
}
