using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PrettyPrompt.Tests
{
    /// <summary>
    /// Generates a stream of random keypresses and evaluates them at the prompt.
    /// </summary>
    [Collection(name: "FuzzingCollection")]
    public class FuzzingTests
    {
        [Theory(Timeout = 60*1000)]
        [InlineData(null)] // random seed, if we find other failing seeds, we can add them here as additional cases
        [InlineData(-1714387066) /* triggered crash when dedenting empty paste */]
        public async Task Fuzz(int? seed)
        {
            seed ??= Guid.NewGuid().GetHashCode();
            var r = new Random(seed.Value);

            var randomKeys = Enumerable.Range(1, 100)
                .Select(_ =>
                {
                    var character = (char)r.Next(32, 127);
                    var key = ConsoleStub.CharToConsoleKey(character);
                    return new ConsoleKeyInfo(character, key,
                        shift: r.NextDouble() > 0.5,
                        alt: r.NextDouble() > 0.5,
                        control: r.NextDouble() > 0.5
                    );
                })
                .Concat(Enumerable.Repeat(new ConsoleKeyInfo('\0', ConsoleKey.Enter, false, false, false), 4)) // hit enter a few times to submit the prompt
                .ToList();

            var console = ConsoleStub.NewConsole();
            console.StubInput(randomKeys);

            var prompt = new Prompt(persistentHistoryFilepath: Path.GetTempFileName(), console: console);

            try
            {
                var result = await prompt.ReadLineAsync("> ");
                Assert.NotNull(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Fuzzing failed for seed: " + seed, ex);
            }
        }
    }

    [CollectionDefinition(
        name: "FuzzingCollection",
        DisableParallelization = true /* disable parallelization so the below test timeout works */
    )] public class FuzzingCollection { }

}
