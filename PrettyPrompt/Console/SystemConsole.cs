using System;

namespace PrettyPrompt.Consoles
{
    /// <summary>
    /// Implementation of <see cref="IConsole"/> that uses the normal <see cref="System.Console"/> APIs
    /// </summary>
    class SystemConsole : IConsole
    {
        public int CursorTop => Console.CursorTop;
        public int BufferWidth => Console.BufferWidth;
        public bool KeyAvailable => Console.KeyAvailable;

        public void Write(string content) => Console.Write(content);
        public void Clear() => Console.Clear();
        public void ShowCursor() => Console.CursorVisible = true;
        public void HideCursor() => Console.CursorVisible = false;
        public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
    }
}
