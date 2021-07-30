#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

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
                var keys = ReadRemainingKeys(console, key);

                if (key.Key == ConsoleKey.Escape)
                {
                    if(MapInputEscapeSequence(keys) is KeyPress ansiEscapedInput)
                    {
                        yield return ansiEscapedInput;
                    }
                }
                else if (keys.Count < 4 || keys.All(k => char.IsControl(k.KeyChar))) // 4 is not special here, just seemed like a decent number to separate
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
        /// On Linux, .NET doesn't map all the ANSI escaped inputs into ConsoleKeyInfos. Map some of the missing ones here.
        /// </summary>
        private static KeyPress MapInputEscapeSequence(List<ConsoleKeyInfo> keys)
        {
            var sequence = new string(keys.Select(key => key.KeyChar).ToArray());
            return sequence switch
            {
                "\u001b1;5P" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F1, shift: false, alt: false, control: true)),
                "\u001b1;5Q" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F2, shift: false, alt: false, control: true)),
                "\u001b1;5R" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F3, shift: false, alt: false, control: true)),
                "\u001b1;5S" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F4, shift: false, alt: false, control: true)),
                "\u001b15;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F5, shift: false, alt: false, control: true)),
                "\u001b17;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F6, shift: false, alt: false, control: true)),
                "\u001b18;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F7, shift: false, alt: false, control: true)),
                "\u001b19;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F8, shift: false, alt: false, control: true)),
                "\u001b20;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F9, shift: false, alt: false, control: true)),
                "\u001b21;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F10, shift: false, alt: false, control: true)),
                "\u001b23;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F11, shift: false, alt: false, control: true)),
                "\u001b24;5~" => new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.F12, shift: false, alt: false, control: true)),
                _ => null
            };
        }

        /// <summary>
        /// Read any remaining key presses in the buffer, including the provided <paramref name="key"/>.
        /// </summary>
        private static List<ConsoleKeyInfo> ReadRemainingKeys(IConsole console, ConsoleKeyInfo key)
        {
            var keys = new List<ConsoleKeyInfo> { key };
            do
            {
                keys.Add(console.ReadKey(true));
            } while (console.KeyAvailable);

            return keys;
        }
    }
}