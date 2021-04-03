using System;

namespace PrettyPrompt.Consoles
{
    internal class KeyPress
    {
        public ConsoleKeyInfo ConsoleKeyInfo { get; }
        public object Pattern { get; }

        public KeyPress(ConsoleKeyInfo consoleKeyInfo)
        {
            this.ConsoleKeyInfo = consoleKeyInfo;
            this.Pattern = consoleKeyInfo.Modifiers == 0
                ? consoleKeyInfo.Key
                : (consoleKeyInfo.Modifiers, consoleKeyInfo.Key);
        }

        public bool Handled { get; internal set; }
    }
}