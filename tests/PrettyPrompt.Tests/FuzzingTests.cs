using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static System.ConsoleKey;

namespace PrettyPrompt.Tests;

/// <summary>
/// Generates a stream of random keypresses and evaluates them at the prompt.
/// </summary>
[Collection(name: "FuzzingCollection")]
public class FuzzingTests
{
    [Theory(Timeout = 60 * 1000)]
    [InlineData(null)] // random seed, if we find other failing seeds, we can add them here as additional cases
    [InlineData(-1714387066)] // triggered crash when dedenting empty paste
    public async Task Fuzz(int? seed)
    {
        seed ??= Guid.NewGuid().GetHashCode();
        var r = new Random(seed.Value);

        var randomKeys = Enumerable.Range(1, 100_000)
            .Select(_ =>
            {
                var character = (char)r.Next(32, 127);
                var key = ConsoleStub.CharToConsoleKey(character);
                return key.ToKeyInfo(character,
                    shift: r.NextDouble() > 0.5,
                    alt: r.NextDouble() > 0.5,
                    control: r.NextDouble() > 0.5
                );
            })
            .Concat(Enumerable.Repeat(Enter.ToKeyInfo('\0'), 4)) // hit enter a few times to submit the prompt
            .ToList();

        var console = ConsoleStub.NewConsole();
        using (console.ProtectClipboard())
        {
            console.StubInput(randomKeys);

            var persistentHistoryFilepath = Path.GetTempFileName();
            try
            {
                await using var prompt = new Prompt(persistentHistoryFilepath, console: console);

                try
                {
                    var result = await prompt.ReadLineAsync();
                    Assert.NotNull(result);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Fuzzing failed for seed: " + seed, ex);
                }
            }
            finally
            {
                File.Delete(persistentHistoryFilepath);
            }
        }
    }

    [Theory(Timeout = 60 * 1000)]
    [InlineData(null)] // random seed, if we find other failing seeds, we can add them here as additional cases
    [InlineData(1786863916)] // triggered crash in SelectionSpan.GetCaretIndices
    public async Task FuzzedSelection(int? seed)
    {
        seed ??= Guid.NewGuid().GetHashCode();
        var r = new Random(seed.Value);

        var directions = new[] { Home, End, LeftArrow, RightArrow, UpArrow, DownArrow };
        var keySet =
            (
                from ctrl in new[] { false, true }
                from shift in new[] { false, true }
                from key in directions
                select key.ToKeyInfo('\0', shift: shift, control: ctrl)
            )
            .Concat(new[]
            {
                A.ToKeyInfo('a'),
                Enter.ToKeyInfo('\0', shift: true),
                Delete.ToKeyInfo('\0')
            })
            .ToArray();

        var randomKeys = Enumerable.Range(1, 10_000)
            .Select(_ => keySet[r.Next(keySet.Length)])
            .Concat(Enumerable.Repeat(Enter.ToKeyInfo('\0'), 4)) // hit enter a few times to submit the prompt
            .ToList();

        var console = ConsoleStub.NewConsole();
        console.StubInput(randomKeys);

        var persistentHistoryFilepath = Path.GetTempFileName();
        try
        {
            await using var prompt = new Prompt(persistentHistoryFilepath, console: console);

            try
            {
                var result = await prompt.ReadLineAsync();
                Assert.NotNull(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Fuzzing failed for seed: " + seed, ex);
            }
        }
        finally
        {
            File.Delete(persistentHistoryFilepath);
        }
    }

    [Theory(Timeout = 60 * 1000)]
    [InlineData(null)] // random seed, if we find other failing seeds, we can add them here as additional cases
    [InlineData(1)] // triggered https://github.com/waf/PrettyPrompt/issues/68
    public async Task FuzzedSelectionAndCompletion(int? seed)
    {
        seed ??= Guid.NewGuid().GetHashCode();
        var r = new Random(seed.Value);

        var directions = new[] { Home, End, LeftArrow, RightArrow, UpArrow, DownArrow };
        var keySet =
            (
                from ctrl in new[] { false, true }
                from shift in new[] { false, true }
                from key in directions
                select key.ToKeyInfo('\0', shift: shift, control: ctrl)
            )
            .Concat(new[]
            {
                A.ToKeyInfo('a'),
                Enter.ToKeyInfo('\0', shift: true),
                Delete.ToKeyInfo('\0'),
                Spacebar.ToKeyInfo('\0', control: true), //completion trigger
            })
            .ToArray();

        var randomKeys = Enumerable.Range(1, 10_000)
            .Select(_ => keySet[r.Next(keySet.Length)])
            .Concat(Enumerable.Repeat(Enter.ToKeyInfo('\0'), 4)) // hit enter a few times to submit the prompt
            .ToList();

        var console = ConsoleStub.NewConsole();
        console.StubInput(randomKeys);

        var persistentHistoryFilepath = Path.GetTempFileName();
        try
        {
            await using var prompt = new Prompt(
            persistentHistoryFilepath,
            callbacks: new TestPromptCallbacks
            {
                CompletionCallback = new CompletionTestData(null).CompletionHandlerAsync
            },
            console: console);

            try
            {
                var result = await prompt.ReadLineAsync();
                Assert.NotNull(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Fuzzing failed for seed: " + seed, ex);
            }
        }
        finally
        {
            File.Delete(persistentHistoryFilepath);
        }
    }

    [Theory(Timeout = 60 * 1000)]
    [InlineData(null)] // random seed, if we find other failing seeds, we can add them here as additional cases
    [InlineData(1)] //triggered different assert failures
    public async Task FuzzedSelectionAndCompletionAndUndoRedo(int? seed)
    {
        seed ??= Guid.NewGuid().GetHashCode();
        var r = new Random(seed.Value);

        var directions = new[] { Home, End, LeftArrow, RightArrow, UpArrow, DownArrow };
        var keySet =
            (
                from ctrl in new[] { false, true }
                from shift in new[] { false, true }
                from key in directions
                select key.ToKeyInfo('\0', shift: shift, control: ctrl)
            )
            .Concat(new[]
            {
                A.ToKeyInfo('a'),
                Enter.ToKeyInfo('\0', shift: true),
                Delete.ToKeyInfo('\0'),
                Spacebar.ToKeyInfo('\0', control: true), //completion trigger
                Z.ToKeyInfo('\0', control: true), //undo
                Y.ToKeyInfo('\0', control: true), //redo
            })
            .ToArray();

        var randomKeys = Enumerable.Range(1, 10_000)
            .Select(_ => keySet[r.Next(keySet.Length)])
            .Concat(Enumerable.Repeat(Enter.ToKeyInfo('\0'), 4)) // hit enter a few times to submit the prompt
            .ToList();

        var console = ConsoleStub.NewConsole();
        console.StubInput(randomKeys);

        var persistentHistoryFilepath = Path.GetTempFileName();
        try
        {
            await using var prompt = new Prompt(
                persistentHistoryFilepath,
                callbacks: new TestPromptCallbacks
                {
                    CompletionCallback = new CompletionTestData(null).CompletionHandlerAsync
                },
                console: console);

            try
            {
                var result = await prompt.ReadLineAsync();
                Assert.NotNull(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Fuzzing failed for seed: " + seed, ex);
            }
        }
        finally
        {
            File.Delete(persistentHistoryFilepath);
        }
    }
}

[CollectionDefinition(
    name: "FuzzingCollection",
    DisableParallelization = true /* disable parallelization so the below test timeout works */
)]
public class FuzzingCollection { }
