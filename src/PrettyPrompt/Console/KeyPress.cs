using System;
using System.Collections.Generic;
using System.Linq;

namespace PrettyPrompt.Consoles
{
    internal class KeyPress
    {
        /// <summary>
        /// The key press as reported by Console.ReadKey
        /// </summary>
        public ConsoleKeyInfo ConsoleKeyInfo { get; }

        /// <summary>
        /// A tuple that represents the key press.
        /// Intended to be pattern matched, e.g. (A) or (Ctrl, A) or (Ctrl | Shift, A) 
        /// </summary>
        public object Pattern { get; }

        /// <summary>
        /// Text that was pasted as a result of this key press.
        /// </summary>
        public string PastedText { get; }

        public KeyPress(ConsoleKeyInfo consoleKeyInfo, string pastedText = null)
        {
            this.ConsoleKeyInfo = consoleKeyInfo;
            this.Pattern = consoleKeyInfo.Modifiers == 0
                ? consoleKeyInfo.Key
                : (consoleKeyInfo.Modifiers, consoleKeyInfo.Key);
            this.PastedText = pastedText;
        }

        public bool Handled { get; internal set; }

        public static IEnumerable<KeyPress> ReadForever(IConsole console)
        {
            while(true)
            {
                var key = console.ReadKey(true);

                if(!console.KeyAvailable)
                {
                    yield return new KeyPress(key);
                    continue;
                }

                // If the user pastes text, we see it as a bunch of key presses. We don't want to send
                // them all individually, as it will trigger syntax highlighting and potentially intellisense
                // for each key press, which is slow. Instead, batch them up to send as single "pasted text" block.
                var keys = ReadRemainingKeys(key);

                if (keys.Count < 4) // 4 is not special here, just seemed like a decent number to separate
                                    // between "keys pressed simultaneously" and "pasted text"
                {
                    foreach (var consoleKey in keys)
                    {
                        yield return new KeyPress(consoleKey);
                    }
                }
                else
                {
                    // we got a bunch of keypresses, send them as a paste event (Shift+Insert)
                    yield return new KeyPress(
                        new ConsoleKeyInfo('\0', ConsoleKey.Insert, true, false, false),
                        pastedText: new string(keys.Select(k => k.KeyChar).ToArray())
                    );
                }
            }
        }

        /// <summary>
        /// Read any remaining key presses in the buffer, including the provided <paramref name="key"/>.
        /// </summary>
        private static List<ConsoleKeyInfo> ReadRemainingKeys(ConsoleKeyInfo key)
        {
            var keys = new List<ConsoleKeyInfo> { key };
            do
            {
                keys.Add(Console.ReadKey(true));
            } while (Console.KeyAvailable);

            return keys;
        }
    }
}