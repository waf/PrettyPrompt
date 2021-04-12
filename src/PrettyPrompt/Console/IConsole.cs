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
        bool KeyAvailable { get; }

        void Write(string content);
        void Clear();
        void ShowCursor();
        void HideCursor();
        ConsoleKeyInfo ReadKey(bool intercept);
        void InitVirtualTerminalProcessing();
    }
}
