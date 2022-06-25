#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Tests;

using static System.ConsoleKey;
using static System.ConsoleModifiers;

public static class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<PromptBenchmark>();

        //For manual running or debugging:
        //var b = new PromptBenchmark();
        //try
        //{
        //    b.Test().Wait();
        //}
        //finally
        //{
        //    b.GlobalCleanup();
        //}
    }
}

[MemoryDiagnoser]
public class PromptBenchmark
{
    private const int PromptSubmits = 100;

    private readonly BenchConsole console;
    private readonly Prompt prompt;
    private readonly string persistentHistoryFilepath;

    public PromptBenchmark()
    {
        var inputs = new List<FormattableString>()
        {
            $"appl{Spacebar}ap{DownArrow}{DownArrow}{UpArrow}{Enter} avxyz{Backspace}{Backspace}{Backspace}oc{Enter}{Shift}{Enter}",
            $"qwerty{Shift}{Home}{Delete}ban{Spacebar}cantaloupe grapef{Spacebar}gr{Enter} ma{DownArrow}{DownArrow}{UpArrow}{UpArrow}{Enter}{Shift}{Enter}",
            $"{Shift}{Enter}{Shift}{Enter}{Shift}{Enter}",
            $"me{Spacebar}o{Spacebar}pear peac{Enter}",
            $"{Enter}", //submit
        };

        console = new BenchConsole(inputs);

        persistentHistoryFilepath = Path.GetTempFileName();
        prompt = new Prompt(
            persistentHistoryFilepath,
            callbacks: new PrettyPrompt.Program.FruitPromptCallbacks(),
            console: console);
    }

    [IterationSetup]
    public void IterationSetup() => console.Reset();

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        File.Delete(persistentHistoryFilepath);
    }

    [Benchmark]
    public async Task Test()
    {
        for (int i = 0; i < PromptSubmits; i++)
        {
            //Console.WriteLine(i);

            var result = await prompt.ReadLineAsync().ConfigureAwait(false);
            Debug.Assert(result.Text ==
                $"apple apricot avocado{Environment.NewLine}" +
                $"banana cantaloupe grapefruit grape mango{Environment.NewLine}" +
                $"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}" +
                $"melon orange pear peach");
        }
    }

    //don't use NSubstite as in unit tests because it's slow
    private class BenchConsole : IConsole
    {
        private readonly ConsoleKeyInfo[] keys;
        private int keyIndex;

        public BenchConsole(List<FormattableString> inputs)
        {
            keys = inputs.SelectMany(i => ConsoleStub.MapToConsoleKeyPresses(i)).ToArray();
        }

        ConsoleKeyInfo IConsole.ReadKey(bool intercept)
        {
            var result = keys[keyIndex];
            keyIndex = (keyIndex + 1) % keys.Length;
            return result;
        }

        public void Reset() => keyIndex = 0;

        int IConsole.CursorTop => 0;
        int IConsole.BufferWidth => 240;
        int IConsole.WindowHeight => 80;
        int IConsole.WindowTop => 0;
        bool IConsole.KeyAvailable => false;
        bool IConsole.CaptureControlC { get => false; set { } }

        event ConsoleCancelEventHandler IConsole.CancelKeyPress { add { } remove { } }

        void IConsole.Clear() { }
        void IConsole.HideCursor() { }
        void IConsole.InitVirtualTerminalProcessing() { }
        void IConsole.ShowCursor() { }
        void IConsole.Write(string? value) { }
        void IConsole.WriteError(string? value) { }
        void IConsole.WriteErrorLine(string? value) { }
        void IConsole.WriteLine(string? value) { }
        void IConsole.Write(ReadOnlySpan<char> value) { }
        void IConsole.WriteError(ReadOnlySpan<char> value) { }
        void IConsole.WriteErrorLine(ReadOnlySpan<char> value) { }
        void IConsole.WriteLine(ReadOnlySpan<char> value) { }
    }
}