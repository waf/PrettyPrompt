#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.IntegrationTests;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException($"Unknown options '{string.Join(" ", args)}'.");

        switch (args[0])
        {
            case "record":
                {
                    Console.Clear();
                    Console.WriteLine("Start typing:");
                    var console = new RecordingConsole();
                    await Run(console);
                    console.Save("record");
                }
                break;

            case "run":
                {
                    var tests = new[]
                    {
                        async () => await Tests.Test_228_229(false),
                        async () => await Tests.Test_228_229(true)
                    };

                    foreach (var test in tests)
                    {
                        Console.Clear();
                        Console.WriteLine("Start typing:");
                        await test();
                    }

                    Console.Clear();
                    Console.WriteLine("All integration tests ran successfully");
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown argument '{args[0]}'.");
        }
    }

    internal static async Task Run(IConsole console)
    {
        var prompt = new Prompt(
            persistentHistoryFilepath: "./history-file",
            callbacks: new PrettyPrompt.Program.FruitPromptCallbacks(),
            console: console,
            configuration: new PromptConfiguration(
                prompt: new FormattedString(">>> ", new FormatSpan(0, 1, AnsiColor.Red), new FormatSpan(1, 1, AnsiColor.Yellow), new FormatSpan(2, 1, AnsiColor.Green)),
                completionItemDescriptionPaneBackground: AnsiColor.Rgb(30, 30, 30),
                selectedCompletionItemBackground: AnsiColor.Rgb(30, 30, 30),
                selectedTextBackground: AnsiColor.Rgb(20, 61, 102)));

        while (true)
        {
            var response = await prompt.ReadLineAsync().ConfigureAwait(false);
            if (response.IsSuccess)
            {
                if (response.Text == "exit") break;
                // optionally, use response.CancellationToken so the user can
                // cancel long-running processing of their response via ctrl-c
                console.WriteLine("You wrote " + (response.SubmitKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) ? response.Text.ToUpper() : response.Text));
            }
        }
    }
}