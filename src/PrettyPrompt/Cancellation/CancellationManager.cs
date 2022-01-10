#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Threading;
using PrettyPrompt.Consoles;

namespace PrettyPrompt.Cancellation;

sealed class CancellationManager
{
    private readonly IConsole console;
    private PromptResult? execution;

    public CancellationManager(IConsole console)
    {
        this.console = console;
        this.console.CancelKeyPress += SignalCancellationToLastResult;
    }

    internal void CaptureControlC()
    {
        this.console.CaptureControlC = true;
    }

    internal void AllowControlCToCancelResult(PromptResult result)
    {
        this.execution = result;
        this.execution.CancellationTokenSource = new CancellationTokenSource();
        this.console.CaptureControlC = false;
    }

    private void SignalCancellationToLastResult(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        this.execution?.CancellationTokenSource?.Cancel();
    }
}
