#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Runtime.InteropServices;

namespace PrettyPrompt.Consoles;

/// <summary>
/// Implementation of <see cref="IConsole"/> that uses the normal <see cref="System.Console"/> APIs
/// </summary>
public class SystemConsole : IConsole
{
    public int CursorTop => Console.CursorTop;
    public int BufferWidth => Console.BufferWidth;
    public int WindowHeight => Console.WindowHeight;
    public int WindowTop => Console.WindowTop;


    public void Write(string value) => Console.Write(value);
    public void WriteLine(string value) => Console.WriteLine(value);
    public void WriteError(string value) => Console.Error.Write(value);
    public void WriteErrorLine(string value) => Console.Error.WriteLine(value);
    public void Clear() => Console.Clear();
    public void ShowCursor() => Console.CursorVisible = true;
    public void HideCursor() => Console.CursorVisible = false;
    public bool KeyAvailable => Console.KeyAvailable;
    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

    public bool CaptureControlC
    {
        get => Console.TreatControlCAsInput;
        set => Console.TreatControlCAsInput = value;
    }

    public event ConsoleCancelEventHandler CancelKeyPress
    {
        add => Console.CancelKeyPress += value;
        remove => Console.CancelKeyPress -= value;
    }

    /// <summary>
    /// Enables ANSI escape codes for controlling the terminal.
    /// https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences
    /// </summary>
    public void InitVirtualTerminalProcessing()
    {
        if (!OperatingSystem.IsWindows()) return;

        // enable writing ansi escape output
        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
        var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
        if (!GetConsoleMode(iStdOut, out uint outConsoleMode) ||
            !SetConsoleMode(iStdOut, outConsoleMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN))
        {
            throw new InvalidOperationException($"failed to set output console mode, error code: {GetLastError()}");
        }
    }

    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GetStdHandle(int nStdHandle);
    [DllImport("kernel32.dll")] private static extern uint GetLastError();
}
