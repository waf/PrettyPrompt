using NSubstitute;
using PrettyPrompt.Consoles;
using System;
using System.Linq;
using Xunit;

namespace PrettyPrompt.Tests
{
    public class KeyPressTests
    {
        [Fact]
        public void KeyPressKeys()
        {
            var console = ConsoleStub.NewConsole();
            var keys = new (FormattableString input, ConsoleKey expectedKey, ConsoleModifiers expectedModifier)[]
            {
                ($"\u001b1;5P", ConsoleKey.F1, ConsoleModifiers.Control),
                ($"\u001b1;5Q", ConsoleKey.F2, ConsoleModifiers.Control),
                ($"\u001b1;5R", ConsoleKey.F3, ConsoleModifiers.Control),
                ($"\u001b1;5S", ConsoleKey.F4, ConsoleModifiers.Control),
                ($"\u001b15;5~", ConsoleKey.F5, ConsoleModifiers.Control),
                ($"\u001b17;5~", ConsoleKey.F6, ConsoleModifiers.Control),
                ($"\u001b18;5~", ConsoleKey.F7, ConsoleModifiers.Control),
                ($"\u001b19;5~", ConsoleKey.F8, ConsoleModifiers.Control),
                ($"\u001b20;5~", ConsoleKey.F9, ConsoleModifiers.Control),
                ($"\u001b21;5~", ConsoleKey.F10, ConsoleModifiers.Control),
                ($"\u001b23;5~", ConsoleKey.F11, ConsoleModifiers.Control),
                ($"\u001b24;5~", ConsoleKey.F12, ConsoleModifiers.Control),
                ($"a", ConsoleKey.A, 0),
                ($"pasted text", ConsoleKey.Insert, ConsoleModifiers.Shift)
            };
            console.StubInput(keys.Select(k => k.input).ToArray());

            var keyAvailableResult = keys
                .SelectMany(key => Enumerable.Repeat(true, key.input.ToString().Length - 1).Append(false))
                .ToArray();
            console
                .KeyAvailable
                .Returns(keyAvailableResult.First(), keyAvailableResult.Skip(1).ToArray());

            var outputKeys = KeyPress.ReadForever(console).Take(keys.Length).ToArray();
            Assert.Equal(keys.Length, outputKeys.Length);
            foreach (var (expectedOutput, output) in keys.Zip(outputKeys))
            {
                Assert.Equal(expectedOutput.expectedKey, output.ConsoleKeyInfo.Key);
                Assert.Equal(expectedOutput.expectedModifier, output.ConsoleKeyInfo.Modifiers);
            }
        }
    }
}
