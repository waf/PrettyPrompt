using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using PrettyPrompt.Consoles;
using NSubstitute;
using NSubstitute.Core;
using static System.ConsoleModifiers;

namespace PrettyPrompt.Tests
{
    public static class ConsoleStub
    {
        private static readonly Regex FormatStringSplit = new Regex(@"({\d+}|.)");

        public static IConsole NewConsole(int width = 100)
        {
            var console = Substitute.For<IConsole>();
            console.BufferWidth.Returns(width);
            return console;
        }

        public static IReadOnlyList<string> GetAllOutput(this IConsole consoleStub) =>
            consoleStub.ReceivedCalls()
                .Where(call => call.GetMethodInfo().Name == nameof(Console.Write))
                .Select(call => (string)call.GetArguments().Single())
                .ToArray();

        public static string GetFinalOutput(this IConsole consoleStub) =>
            consoleStub.GetAllOutput()[^2]; // second to last. The last is always the newline drawn after the prompt is submitted

        /// <summary>
        /// Stub Console.ReadKey to return a series of keystrokes (<see cref="ConsoleKeyInfo" />).
        /// Keystrokes are specified as a <see cref="System.FormattableString"/> with any special keys,
        /// like modifiers or navigation keys, represented as FormattableString arguments (of type
        /// <see cref="ConsoleModifiers"/> or <see cref="ConsoleKey"/>).
        /// </summary>
        /// <example>$"{Control}LHello{Enter}" is turned into Ctrl-L, H, e, l, l, o, Enter key</example>
        public static ConfiguredCall StubInput(this IConsole consoleStub, params FormattableString[] inputs)
        {
            List<ConsoleKeyInfo> keys = inputs
                .SelectMany(line => MapToConsoleKeyPresses(line))
                .ToList();

            consoleStub
                .KeyAvailable
                .Returns(true);

            return consoleStub
                .ReadKey(intercept: true)
                .Returns(keys.First(), keys.Skip(1).ToArray());
        }

        private static List<ConsoleKeyInfo> MapToConsoleKeyPresses(FormattableString input)
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
            list.Add(ToConsoleKeyInfo(modifiersPressed, (ConsoleKey)char.ToUpper(keyChar), keyChar));
            return 0;
        }

        private static ConsoleModifiers AppendFormatStringArgument(List<ConsoleKeyInfo> list, Match key, ConsoleModifiers modifiersPressed, object formatArgument)
        {
            switch (formatArgument)
            {
                case ConsoleModifiers modifier:
                    return modifiersPressed | modifier;
                case ConsoleKey consoleKey:
                    var parsed = char.TryParse(key.Value, out char character);
                    list.Add(ToConsoleKeyInfo(modifiersPressed, consoleKey, parsed ? character : MapSpecialKey(consoleKey)));
                    return 0;
                default: throw new ArgumentException("Unknown value: " + formatArgument, nameof(formatArgument));
            }
        }

        private static char MapSpecialKey(ConsoleKey consoleKey) =>
            consoleKey switch
            {
                ConsoleKey.Backspace => '\b',
                ConsoleKey.Tab => '\t',
                ConsoleKey.Spacebar => ' ',
                _ => '\0' // home, enter, arrow keys, etc
            };

        private static ConsoleKeyInfo ToConsoleKeyInfo(ConsoleModifiers modifiersPressed, ConsoleKey consoleKey, char character) =>
            new ConsoleKeyInfo(
                character, consoleKey,
                modifiersPressed.HasFlag(Shift), modifiersPressed.HasFlag(Alt), modifiersPressed.HasFlag(Control)
            );
    }
}

