#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrettyPrompt.IntegrationTests;

internal class Tests
{
    public static async Task Test1()
    {
        //TODO
        CheckOutputLogs(Array.Empty<string>(), Array.Empty<string>());
        await Task.Delay(0);
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
}