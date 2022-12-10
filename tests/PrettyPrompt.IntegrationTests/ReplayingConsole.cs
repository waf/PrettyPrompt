#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

internal class ReplayingConsole : RecordingConsole
{
    private readonly ConsoleKeyInfo[] pressedKeys;
    private int keyIndex;

    public IReadOnlyList<string> RecordedOutputLog { get; }
    public IReadOnlyList<string> ReplayOutputLog => outputLog;

    public override void Write(ReadOnlySpan<char> value) => base.Write(value);
    public override void WriteLine(ReadOnlySpan<char> value) => base.WriteLine(value);
    public override void WriteError(ReadOnlySpan<char> value) => base.WriteError(value);
    public override void WriteErrorLine(ReadOnlySpan<char> value) => base.WriteErrorLine(value);
    public override void Clear() => base.Clear();

    public unsafe ReplayingConsole(string path)
    {
        pressedKeys = File.ReadAllLines(path + InputExtension)
            .Select(l =>
            {
                var parts = l.Split(' ');
                var charData = Convert.FromBase64String(parts[0]);
                char character;
                fixed (byte* p = charData) character = *(char*)p;
                return new ConsoleKeyInfo(
                    character,
                    Enum.Parse<ConsoleKey>(parts[1]),
                    bool.Parse(parts[2]),
                    bool.Parse(parts[3]),
                    bool.Parse(parts[4]));
            })
            .ToArray();

        RecordedOutputLog = File.ReadAllLines(path + OutputExtension).ToImmutableArray();
    }

    public override ConsoleKeyInfo ReadKey(bool intercept)
    {
        if (!intercept) throw new NotSupportedException();

        var key = pressedKeys[keyIndex++];
        outputLog.Add($"ReadKey({intercept}): {key.Key}");

        return key;
    }
}