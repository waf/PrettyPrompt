#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using TextCopy;

namespace PrettyPrompt.Consoles;

/// <summary>
/// Console abstraction, mainly for testability.
/// In the real application it will be the System.Console APIs.
/// </summary>
public interface IConsole
{
    int CursorTop { get; }
    int BufferWidth { get; }
    int WindowHeight { get; }
    int WindowTop { get; }

    void Write(string value);
    void WriteLine(string value);
    void WriteError(string value);
    void WriteErrorLine(string value);
    void Clear();
    void ShowCursor();
    void HideCursor();
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey(bool intercept);
    void InitVirtualTerminalProcessing();

    event ConsoleCancelEventHandler CancelKeyPress;
    bool CaptureControlC { get; set; }
}

internal interface IConsoleWithClipboard : IConsole
{
    IClipboard Clipboard { get; }
    IDisposable ProtectClipboard();
}