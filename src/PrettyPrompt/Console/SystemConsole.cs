using System;
using System.Runtime.InteropServices;

namespace PrettyPrompt.Consoles
{
    /// <summary>
    /// Implementation of <see cref="IConsole"/> that uses the normal <see cref="System.Console"/> APIs
    /// </summary>
    public class SystemConsole : IConsole
    {
        public SystemConsole()
        {
            Console.TreatControlCAsInput = true;
        }

        public int CursorTop => Console.CursorTop;
        public int BufferWidth => Console.BufferWidth;
        public int WindowHeight => Console.WindowHeight;
        public int WindowTop => Console.WindowTop;

        public void Write(string content) => Console.Write(content);
        public void Clear() => Console.Clear();
        public void ShowCursor() => Console.CursorVisible = true;
        public void HideCursor() => Console.CursorVisible = false;
        public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

        /// <summary>
        /// Enables ANSI escape codes for controlling the terminal.
        /// https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences
        /// </summary>
        public void InitVirtualTerminalProcessing()
        {
            if (!OperatingSystem.IsWindows()) return;

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
}
