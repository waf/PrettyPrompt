using System;

namespace PrettyPrompt.Consoles
{
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

        void Write(string content);
        void Clear();
        void ShowCursor();
        void HideCursor();
        bool KeyAvailable { get; }
        ConsoleKeyInfo ReadKey(bool intercept);
        void InitVirtualTerminalProcessing();

        event ConsoleCancelEventHandler CancelKeyPress;
        bool CaptureControlC { get; set; }
    }
}
