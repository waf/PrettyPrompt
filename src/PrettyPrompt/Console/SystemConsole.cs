#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Runtime.InteropServices;

namespace PrettyPrompt.Consoles;

/// <summary>
/// Implementation of <see cref="IConsole"/> that uses the normal <see cref="Console"/> APIs
/// </summary>
public class SystemConsole : IConsole
{
    public int CursorTop => Console.CursorTop;
    public int BufferWidth => Console.BufferWidth;
    public int WindowHeight => Console.WindowHeight;
    public int WindowTop => Console.WindowTop;

    public void Write(string? value) => Write(value.AsSpan());
    public void WriteLine(string? value) => WriteLine(value.AsSpan());
    public void WriteError(string? value) => WriteError(value.AsSpan());
    public void WriteErrorLine(string? value) => WriteErrorLine(value.AsSpan());

    public virtual void Write(ReadOnlySpan<char> value) => Console.Out.Write(value);
    public virtual void WriteLine(ReadOnlySpan<char> value) => Console.Out.WriteLine(value);
    public virtual void WriteError(ReadOnlySpan<char> value) => Console.Error.Write(value);
    public virtual void WriteErrorLine(ReadOnlySpan<char> value) => Console.Error.WriteLine(value);

    public virtual void Clear() => Console.Clear();
    public void ShowCursor() => Console.CursorVisible = true;
    public void HideCursor() => Console.CursorVisible = false;
    public bool KeyAvailable => Console.KeyAvailable;
    public virtual ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

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
            throw new InvalidOperationException($"failed to set output console mode, error code: {Marshal.GetLastWin32Error()}");
        }
    }

    [DllImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    /// <summary>
    /// Returned handle does not need to be closed (https://docs.microsoft.com/en-us/windows/console/getstdhandle#handle-disposal).
    /// </summary>
    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);
}