#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.Core;
using PrettyPrompt.Consoles;
using TextCopy;

namespace PrettyPrompt.Tests;

internal static class ConsoleStub
{
    private static readonly Regex FormatStringSplit = new(@"({\d+}|.)", RegexOptions.Compiled);
    private static readonly Semaphore semaphore = new(1, 1, nameof(ConsoleStub) + "Semaphore"); //interprocess
    private static bool isClipboardProtected;

    public static IConsoleWithClipboard NewConsole(int width = 100, int height = 100)
    {
        var console = Substitute.For<IConsoleWithClipboard>();
        console.BufferWidth.Returns(width);
        console.WindowHeight.Returns(height);
        console.Clipboard.Returns(new ProtectedClipboard());

        console.ProtectClipboard().Returns(
            _ =>
            {
                semaphore.WaitOne();
                if (isClipboardProtected) throw new InvalidOperationException("Clipboard is already protected.");
                isClipboardProtected = true;
                return new MutexProtector();
            });

        return console;
    }

    public static IReadOnlyList<string> GetAllOutput(this IConsole consoleStub) =>
        consoleStub.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(Console.Write))
            .Select(call =>
            {
                var arg = (string?)call.GetArguments().Single();
                Debug.Assert(arg != null);
                return arg;
            })
            .ToArray();

    public static string GetFinalOutput(this IConsole consoleStub)
    {
        return consoleStub.GetAllOutput()[^2]; // second to last. The last is always the newline drawn after the prompt is submitted
    }

    /// <summary>
    /// Stub Console.ReadKey to return a series of keystrokes (<see cref="ConsoleKeyInfo" />).
    /// Keystrokes are specified as a <see cref="FormattableString"/> with any special keys,
    /// like modifiers or navigation keys, represented as FormattableString arguments (of type
    /// <see cref="ConsoleModifiers"/> or <see cref="ConsoleKey"/>).
    /// </summary>
    /// <example>$"{Control}LHello{Enter}" is turned into Ctrl-L, H, e, l, l, o, Enter key</example>
    public static ConfiguredCall StubInput(this IConsole consoleStub, params FormattableString[] inputs)
    {
        var keys = inputs
            .SelectMany(line => MapToConsoleKeyPresses(line))
            .ToList();

        return consoleStub.StubInput(keys);
    }

    /// <summary>
    /// Stub Console.ReadKey to return a series of keystrokes (<see cref="ConsoleKeyInfo" />).
    /// Keystrokes are specified as a <see cref="FormattableString"/> with any special keys,
    /// like modifiers or navigation keys, represented as FormattableString arguments (of type
    /// <see cref="ConsoleModifiers"/> or <see cref="ConsoleKey"/>) and with optional Action to be invoked after key press.
    /// Use <see cref="Input(FormattableString)" and <see cref="Input(FormattableString, Action)"/> methods to create inputs./>
    /// </summary>
    public static ConfiguredCall StubInput(this IConsole consoleStub, params FormattableStringWithAction[] inputs)
    {
        var keys = inputs
            .SelectMany(EnumerateKeys)
            .ToList();

        return consoleStub
            .ReadKey(intercept: true)
            .Returns(keys.First(), keys.Skip(1).ToArray());

        IEnumerable<Func<CallInfo, ConsoleKeyInfo>> EnumerateKeys(FormattableStringWithAction input)
        {
            var keyPresses = MapToConsoleKeyPresses(input.Input);
            if (keyPresses.Count > 0)
            {
                for (int i = 0; i < keyPresses.Count - 1; i++)
                {
                    int index = i; //copy for closure (important!)
                    yield return _ => keyPresses[index];
                }
                yield return _ =>
                {
                    input.ActionAfter?.Invoke();
                    return keyPresses[^1];
                };
            }
            else if (input.ActionAfter != null)
            {
                throw new InvalidOperationException("you can specify 'actionAfter' only after keyPress");
            }
        }
    }

    public static ConfiguredCall StubInput(this IConsole consoleStub, List<ConsoleKeyInfo> keys)
    {
        return consoleStub
            .ReadKey(intercept: true)
            .Returns(keys.First(), keys.Skip(1).ToArray());
    }

    internal static List<ConsoleKeyInfo> MapToConsoleKeyPresses(FormattableString input)
    {
        ConsoleModifiers modifiersPressed = 0;
        // split the formattable strings into a mix of format placeholders (e.g. {0}, {1}) and literal characters.
        // For the format placeholders, we can get the arguments as their original objects (ConsoleModifiers or ConsoleKey).
        return FormatStringSplit
            .Matches(input.Format)
            .Aggregate(
                seed: new List<ConsoleKeyInfo>(),
                func: (list, key) =>
                {
                    if (key.Value.StartsWith('{') && key.Value.EndsWith('}'))
                    {
                        var formatArgument = input.GetArgument(int.Parse(key.Value.Trim('{', '}')));
                        modifiersPressed = AppendFormatStringArgument(list, key, modifiersPressed, formatArgument);
                    }
                    else
                    {
                        modifiersPressed = AppendLiteralKey(list, key.Value.Single(), modifiersPressed);
                    }

                    return list;
                }
            );
    }

    private static ConsoleModifiers AppendLiteralKey(List<ConsoleKeyInfo> list, char keyChar, ConsoleModifiers modifiersPressed)
    {
        list.Add(CharToConsoleKey(keyChar).ToKeyInfo(keyChar, modifiersPressed));
        return 0;
    }

    public static ConsoleKey CharToConsoleKey(char keyChar) =>
        keyChar switch
        {
            '.' => ConsoleKey.OemPeriod,
            ',' => ConsoleKey.OemComma,
            '-' => ConsoleKey.OemMinus,
            '+' => ConsoleKey.OemPlus,
            '\'' => ConsoleKey.Oem7,
            '/' => ConsoleKey.Divide,
            '!' => ConsoleKey.D1,
            '@' => ConsoleKey.D2,
            '#' => ConsoleKey.D3,
            '$' => ConsoleKey.D4,
            '%' => ConsoleKey.D5,
            '^' => ConsoleKey.D6,
            '&' => ConsoleKey.D7,
            '*' => ConsoleKey.D8,
            '(' => ConsoleKey.D9,
            ')' => ConsoleKey.D0,
            <= (char)255 => (ConsoleKey)char.ToUpper(keyChar),
            _ => ConsoleKey.Oem1
        };

    private static ConsoleModifiers AppendFormatStringArgument(List<ConsoleKeyInfo> list, Match key, ConsoleModifiers modifiersPressed, object? formatArgument)
    {
        switch (formatArgument)
        {
            case ConsoleModifiers modifier:
                return modifiersPressed | modifier;
            case ConsoleKey consoleKey:
                var parsed = char.TryParse(key.Value, out char character);
                list.Add(consoleKey.ToKeyInfo(parsed ? character : MapSpecialKey(consoleKey), modifiersPressed));
                return 0;
            case char c:
                list.Add(CharToConsoleKey(c).ToKeyInfo(c, modifiersPressed));
                return 0;
            case string text:
                if (text.Length > 0)
                {
                    list.Add(CharToConsoleKey(text[0]).ToKeyInfo(text[0], modifiersPressed));
                }
                for (int i = 1; i < text.Length; i++)
                {
                    list.Add(CharToConsoleKey(text[i]).ToKeyInfo(text[i], 0));
                }
                return 0;
            default: throw new ArgumentException("Unknown value: " + formatArgument, nameof(formatArgument));
        }
    }

    private static char MapSpecialKey(ConsoleKey consoleKey) =>
        consoleKey switch
        {
            ConsoleKey.Backspace => '\b',
            ConsoleKey.Tab => '\t',
            ConsoleKey.Oem7 => '\'',
            ConsoleKey.Spacebar => ' ',
            _ => '\0' // home, enter, arrow keys, etc
        };

    public static FormattableStringWithAction Input(FormattableString input) => new(input);
    public static FormattableStringWithAction Input(FormattableString input, Action actionAfter) => new(input, actionAfter);

    public readonly struct FormattableStringWithAction
    {
        public readonly FormattableString Input;
        public readonly Action? ActionAfter;

        public FormattableStringWithAction(FormattableString input)
            : this(input, null) { }

        public FormattableStringWithAction(FormattableString input, Action? actionAfter)
        {
            Input = input;
            ActionAfter = actionAfter;
        }
    }

    private class MutexProtector : IDisposable
    {
        public void Dispose()
        {
            isClipboardProtected = false;
            semaphore.Release();
        }
    }

    private class ProtectedClipboard : IClipboard
    {
        private readonly IClipboard clipboard = new Clipboard();

        public string? GetText()
        {
            Check();
            return clipboard.GetText();
        }

        public Task<string?> GetTextAsync(CancellationToken cancellation = default)
        {
            Check();
            return clipboard.GetTextAsync(cancellation);
        }

        public void SetText(string text)
        {
            Check();
            clipboard.SetText(text);
        }

        public Task SetTextAsync(string text, CancellationToken cancellation = default)
        {
            Check();
            return clipboard.SetTextAsync(text, cancellation);
        }

        private static void Check()
        {
            if (!isClipboardProtected)
                throw new InvalidOperationException("If the test uses clipboard the test has to be wrapped by ProtectClipboard() method call.");
        }
    }
}
