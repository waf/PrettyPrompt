#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PrettyPrompt.Consoles;

[DebuggerDisplay("{ObjectPattern}")]
public class KeyPress
{
    /// <summary>
    /// The key press as reported by Console.ReadKey
    /// </summary>
    public ConsoleKeyInfo ConsoleKeyInfo { get; }

    /// <summary>
    /// A tuple that represents the key press.
    /// Intended to be pattern matched, e.g. (A) or (Ctrl, A) or (Ctrl | Shift, A).
    /// It's either <see cref="ConsoleKey"/> or (<see cref="ConsoleModifiers"/>, <see cref="ConsoleKey"/>).
    /// </summary>
    public object ObjectPattern { get; }

    /// <summary>
    /// Text that was pasted as a result of this key press.
    /// </summary>
    public string? PastedText { get; }

    internal bool Handled { get; set; }

    public KeyPress(ConsoleKeyInfo consoleKeyInfo, string? pastedText = null)
    {
        if (consoleKeyInfo is { Key: ConsoleKey.Enter, KeyChar: '\r' })
        {
            ConsoleKeyInfo = new ConsoleKeyInfo(
                '\n', //'\r' is unexpected and makes problems (e.g. https://github.com/waf/CSharpRepl/issues/213)
                ConsoleKey.Enter,
                consoleKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift),
                consoleKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt),
                consoleKeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control));
        }
        else
        {
            ConsoleKeyInfo = consoleKeyInfo;
        }

        ObjectPattern =
            consoleKeyInfo.Modifiers == 0 ?
            consoleKeyInfo.Key :
            (consoleKeyInfo.Modifiers, consoleKeyInfo.Key);

        PastedText = pastedText;
    }

    internal static IEnumerable<KeyPress> ReadForever(IConsole console)
    {
        while (true)
        {
            var key = console.ReadKey(true);

            if (!console.KeyAvailable)
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
                if (MapInputEscapeSequence(keys) is KeyPress ansiEscapedInput)
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
                    ConsoleKey.Insert.ToKeyInfo('\0', shift: true),
                    pastedText: new string(keys.Select(k => k.KeyChar).ToArray())
                );
            }
        }
    }

    /// <summary>
    /// On Linux, .NET doesn't map all the ANSI escaped inputs into ConsoleKeyInfos. Map some of the missing ones here.
    /// </summary>
    private static KeyPress? MapInputEscapeSequence(List<ConsoleKeyInfo> keys)
    {
        var sequence = new string(keys.Select(key => key.KeyChar).ToArray());

        // xterm "modifyOtherKeys" reports otherwise-ambiguous combinations (Shift/Ctrl/Alt + Enter, which
        // all normally arrive as a bare CR) as ESC [ 27 ; <modifier> ; <keycode> ~. Handle it before the
        // literal lookups below, since the modifier and keycode vary. See AnsiEscapeCodes.EnableModifyOtherKeys.
        if (TryMapModifyOtherKeys(sequence, out var modifiedKeyPress))
        {
            return modifiedKeyPress;
        }

        return sequence switch
        {
            "\u001b1;5P" => new KeyPress(ConsoleKey.F1.ToKeyInfo('\0', control: true)),
            "\u001b1;5Q" => new KeyPress(ConsoleKey.F2.ToKeyInfo('\0', control: true)),
            "\u001b1;5R" => new KeyPress(ConsoleKey.F3.ToKeyInfo('\0', control: true)),
            "\u001b1;5S" => new KeyPress(ConsoleKey.F4.ToKeyInfo('\0', control: true)),
            "\u001b15;5~" => new KeyPress(ConsoleKey.F5.ToKeyInfo('\0', control: true)),
            "\u001b17;5~" => new KeyPress(ConsoleKey.F6.ToKeyInfo('\0', control: true)),
            "\u001b18;5~" => new KeyPress(ConsoleKey.F7.ToKeyInfo('\0', control: true)),
            "\u001b19;5~" => new KeyPress(ConsoleKey.F8.ToKeyInfo('\0', control: true)),
            "\u001b20;5~" => new KeyPress(ConsoleKey.F9.ToKeyInfo('\0', control: true)),
            "\u001b21;5~" => new KeyPress(ConsoleKey.F10.ToKeyInfo('\0', control: true)),
            "\u001b23;5~" => new KeyPress(ConsoleKey.F11.ToKeyInfo('\0', control: true)),
            "\u001b24;5~" => new KeyPress(ConsoleKey.F12.ToKeyInfo('\0', control: true)),
            _ => null
        };
    }

    /// <summary>
    /// Parses an xterm "modifyOtherKeys" CSI 27 sequence of the form ESC [ 27 ; modifier ; keycode ~.
    /// The modifier is encoded as 1 + a bitmask of Shift(1), Alt(2), Control(4); the keycode is the Unicode
    /// value of the base key. We enable level 1 (see <see cref="AnsiEscapeCodes.EnableModifyOtherKeys"/>), so we
    /// only see combinations the terminal would otherwise have to encode ambiguously (Shift/Ctrl/Alt+Enter,
    /// Ctrl/Alt+letter, ...). We decode all of them rather than just Enter, so none get dropped - since we asked
    /// the terminal to emit these, anything we didn't translate would otherwise vanish. .NET's Unix console parser
    /// strips the leading '[' (compare the function-key sequences above, which also lack it), so both are accepted.
    /// </summary>
    private static bool TryMapModifyOtherKeys(string sequence, [NotNullWhen(true)] out KeyPress? keyPress)
    {
        keyPress = null;

        string body;
        if (sequence.StartsWith("[27;", StringComparison.Ordinal)) body = sequence[2..];
        else if (sequence.StartsWith("27;", StringComparison.Ordinal)) body = sequence[1..];
        else return false;

        if (!body.EndsWith("~", StringComparison.Ordinal)) return false;

        var parts = body[..^1].Split(';'); // ["27", "<modifier>", "<keycode>"]
        if (parts.Length != 3 ||
            !int.TryParse(parts[1], out var modifier) ||
            !int.TryParse(parts[2], out var keyCode))
        {
            return false;
        }

        var modifierMask = modifier - 1;
        var shift = (modifierMask & 1) != 0;
        var alt = (modifierMask & 2) != 0;
        var control = (modifierMask & 4) != 0;

        var (key, keyChar) = MapModifyOtherKeysCode(keyCode, shift, control);
        // For Enter, the KeyPress constructor normalizes the '\r' KeyChar to '\n' while preserving the modifiers.
        keyPress = new KeyPress(new ConsoleKeyInfo(keyChar, key, shift, alt, control));
        return true;
    }

    /// <summary>
    /// Maps a modifyOtherKeys keycode (the Unicode value of the base key) to a <see cref="ConsoleKey"/> and the
    /// glyph to insert. Bindings driven by these (Ctrl/Alt+letter, Enter, ...) match on Key+Modifiers, so the
    /// glyph only matters for keys that actually produce text. Because we enable level 1, plain Shift+printable
    /// is left to the keyboard layout and won't arrive here - so we never reconstruct shifted punctuation (which
    /// we couldn't without layout knowledge); letters are upper-cased for the bare-Shift case just in case.
    /// </summary>
    private static (ConsoleKey Key, char KeyChar) MapModifyOtherKeysCode(int code, bool shift, bool control)
    {
        switch (code)
        {
            case 13: return (ConsoleKey.Enter, '\r');
            case 9: return (ConsoleKey.Tab, '\t');
            case 27: return (ConsoleKey.Escape, (char)27);
            case 8 or 127: return (ConsoleKey.Backspace, '\b');
            case 32: return (ConsoleKey.Spacebar, ' ');
        }

        // Control combinations are matched on Key+Modifiers, so their glyph is irrelevant - use '\0'. Otherwise
        // pass the character through, upper-casing a bare-shifted letter so it still types as a capital.
        if (code is >= 'a' and <= 'z') return (ConsoleKey.A + (code - 'a'), control ? '\0' : (char)(shift ? code - 32 : code));
        if (code is >= 'A' and <= 'Z') return (ConsoleKey.A + (code - 'A'), control ? '\0' : (char)code);
        if (code is >= '0' and <= '9') return ((ConsoleKey)code, control ? '\0' : (char)code); // ConsoleKey.D0..D9 == '0'..'9'
        return (default, control ? '\0' : (char)code);
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
