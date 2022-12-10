#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using PrettyPrompt.Consoles;

internal class RecordingConsole : SystemConsole
{
    protected const string InputExtension = ".input.txt";
    protected const string OutputExtension = ".output.txt";

    private readonly List<ConsoleKeyInfo> pressedKeys = new();
    protected readonly List<string> outputLog = new();

    private int lastBufferWidth, lastBufferHeight;
    private int lastCursorLeft, lastCursorTop;
    private int lastWindowWidth, lastWindowHeight;
    private int lastWindowLeft, lastWindowTop;

    public RecordingConsole()
    {
        lastBufferWidth = BufferWidth;
        outputLog.Add($"{nameof(BufferWidth)}={lastBufferWidth}");

        lastBufferHeight = Console.BufferHeight;
        outputLog.Add($"{nameof(Console.BufferHeight)}={lastBufferHeight}");

        lastCursorLeft = Console.CursorLeft;
        outputLog.Add($"{nameof(Console.CursorLeft)}={lastCursorLeft}");

        lastCursorTop = CursorTop;
        outputLog.Add($"{nameof(CursorTop)}={lastCursorTop}");

        lastWindowWidth = Console.WindowWidth;
        outputLog.Add($"{nameof(Console.WindowWidth)}={lastWindowWidth}");

        lastWindowHeight = WindowHeight;
        outputLog.Add($"{nameof(WindowHeight)}={lastWindowHeight}");

        lastWindowLeft = Console.WindowLeft;
        outputLog.Add($"{nameof(Console.WindowLeft)}={lastWindowLeft}");

        lastWindowTop = WindowTop;
        outputLog.Add($"{nameof(WindowTop)}={lastWindowTop}");
    }

    public override void Write(ReadOnlySpan<char> value)
    {
        TrackConsoleChanges(true);
        outputLog.Add($"Write(\"{Escape(value)}\")");
        base.Write(value);
        TrackConsoleChanges(false);
    }

    public override void WriteLine(ReadOnlySpan<char> value)
    {
        TrackConsoleChanges(true);
        outputLog.Add($"WriteLine(\"{Escape(value)}\")");
        base.WriteLine(value);
        TrackConsoleChanges(false);
    }

    public override void WriteError(ReadOnlySpan<char> value)
    {
        TrackConsoleChanges(true);
        outputLog.Add($"WriteError(\"{Escape(value)}\")");
        base.WriteError(value);
        TrackConsoleChanges(false);
    }

    public override void WriteErrorLine(ReadOnlySpan<char> value)
    {
        TrackConsoleChanges(true);
        outputLog.Add($"WriteErrorLine(\"{Escape(value)}\")");
        base.WriteErrorLine(value);
        TrackConsoleChanges(false);
    }

    public override void Clear()
    {
        TrackConsoleChanges(true);
        outputLog.Add($"Clear()");
        base.Clear();
        TrackConsoleChanges(false);
    }

    public override ConsoleKeyInfo ReadKey(bool intercept)
    {
        TrackConsoleChanges(true);
        if (!intercept) throw new NotSupportedException();

        var key = base.ReadKey(intercept);
        pressedKeys.Add(key);
        outputLog.Add($"ReadKey({intercept}): {key.Key}");
        TrackConsoleChanges(false);
        return key;
    }

    public void Save(string path)
    {
        File.WriteAllLines(
            path + InputExtension,
            pressedKeys.Select(k => $"{C(k)} {k.Key} {Mod(k, ConsoleModifiers.Shift)} {Mod(k, ConsoleModifiers.Alt)} {Mod(k, ConsoleModifiers.Control)}"));

        File.WriteAllLines(
            path + OutputExtension,
            outputLog);

        static bool Mod(ConsoleKeyInfo key, ConsoleModifiers modifier) => key.Modifiers.HasFlag(modifier);
        static unsafe string C(ConsoleKeyInfo key)
        {
            var c = key.KeyChar;
            return Convert.ToBase64String(new Span<byte>((byte*)&c, sizeof(char)));
        }
    }

    private void TrackConsoleChanges(bool isBefore, [CallerMemberName] string methodName = "")
    {
        var changes = new List<string>();

        Track(ref lastBufferWidth, BufferWidth, nameof(BufferWidth));
        Track(ref lastBufferHeight, Console.BufferHeight, nameof(Console.BufferHeight));

        Track(ref lastCursorLeft, Console.CursorLeft, nameof(Console.CursorLeft));
        Track(ref lastCursorTop, CursorTop, nameof(CursorTop));

        Track(ref lastWindowWidth, Console.WindowWidth, nameof(Console.WindowWidth));
        Track(ref lastWindowHeight, WindowHeight, nameof(WindowHeight));

        Track(ref lastWindowLeft, Console.WindowLeft, nameof(Console.WindowLeft));
        Track(ref lastWindowTop, WindowTop, nameof(WindowTop));

        if (changes.Count > 0)
        {
            outputLog.Add($"{(isBefore ? "Before" : "After")} {methodName}:");
            foreach (var change in changes)
            {
                outputLog.Add("\t" + change);
            }
        }

        void Track(ref int lastValue, int currentValue, string name)
        {
            if (currentValue != lastValue)
            {
                changes.Add($"{name}: {lastValue} -> {currentValue}");
                lastValue = currentValue;
            }
        }
    }

    private static string Escape(ReadOnlySpan<char> value)
    {
        return value.ToString()
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0")
            .Replace("\u001b", "#");
    }
}