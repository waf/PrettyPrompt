#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PrettyPrompt.IntegrationTests;

internal class Tests
{
    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/228
    /// https://github.com/waf/PrettyPrompt/issues/229
    /// </summary>
    public static async Task Test_228_229(bool biggerBufferThanWindow)
    {
        if (OperatingSystem.IsWindows())
        {
            Console.WindowWidth = 120;
            Console.WindowHeight = 30;
            Console.BufferWidth = 120;
            Console.BufferHeight = biggerBufferThanWindow ? 3000 : 30;
        }

        var console = new ReplayingConsole(GetDataPath("record#228#229" + (biggerBufferThanWindow ? "a" : "b")));
        await Program.Run(console);
        console.Save("record#actual"); //for debuging
        CheckOutputLogs(console.ReplayOutputLog, console.RecordedOutputLog);
    }

    private static void CheckOutputLogs(IReadOnlyList<string> replayOutputLog, IReadOnlyList<string> recordedOutputLog)
    {
        if (replayOutputLog.Count != recordedOutputLog.Count) Throw();
        for (int i = 0; i < replayOutputLog.Count; i++)
        {
            if (replayOutputLog[i] != recordedOutputLog[i]) Throw();
        }

        static void Throw() => throw new InvalidOperationException("Recorded (=expected) output log and output log of recorded input replay (=actual) differs.");
    }

    private static string GetDataPath(string name) => Path.Combine("..", "..", "..", "Data", name);
}