#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PrettyPrompt.Consoles;

internal readonly struct ConsoleCoordinate : IEquatable<ConsoleCoordinate>
{
    public static ConsoleCoordinate Zero => default;

    public readonly int Row;
    public readonly int Column;

    public ConsoleCoordinate(int row, int column)
    {
        Debug.Assert(row >= 0);
        Debug.Assert(column >= 0);

        Row = row;
        Column = column;
    }

    public ConsoleCoordinate MoveUp() => new(Math.Max(0, Row - 1), Column);
    public ConsoleCoordinate MoveDown() => new(Row + 1, Column);
    public ConsoleCoordinate MoveLeft() => new(Row, Math.Max(0, Column - 1));
    public ConsoleCoordinate MoveRight() => new(Row, Column + 1);

    public ConsoleCoordinate WithRow(int row) => new(row, Column);
    public ConsoleCoordinate WithColumn(int column) => new(Row, column);

    public ConsoleCoordinate Offset(int rowOffset, int columnOffset) => new(Row + rowOffset, Column + columnOffset);

    public static bool operator ==(ConsoleCoordinate left, ConsoleCoordinate right) => left.Equals(right);
    public static bool operator !=(ConsoleCoordinate left, ConsoleCoordinate right) => !(left == right);

    public static bool operator <(ConsoleCoordinate left, ConsoleCoordinate right)
        => left.Row == right.Row ? left.Column < right.Column : left.Row < right.Row;

    public static bool operator <=(ConsoleCoordinate left, ConsoleCoordinate right)
        => left.Row == right.Row ? left.Column <= right.Column : left.Row <= right.Row;

    public static bool operator >(ConsoleCoordinate left, ConsoleCoordinate right) => !(left <= right);
    public static bool operator >=(ConsoleCoordinate left, ConsoleCoordinate right) => !(left < right);

    public override bool Equals(object? obj) => obj is ConsoleCoordinate other && Equals(other);
    public bool Equals(ConsoleCoordinate other) => Row == other.Row && Column == other.Column;
    public bool Equals(int row, int column) => Row == row && Column == column;
    public override int GetHashCode() => HashCode.Combine(Row, Column);
    public override string ToString() => $"Row: {Row}, Column: {Column}";

    public int ToCaret(IReadOnlyList<string> lines)
    {
        Debug.Assert(lines.All(l => l.All(c => c != '\r')));

        if (Row >= lines.Count)
        {
            Debug.Fail("inconsitent position and lines");
            return lines.Select(l => l.Length).Sum();
        }

        int caret = 0;
        for (int i = 0; i < Row; i++) caret += lines[i].Length + 1; // +1 for '\n'

        if (Column <= lines[Row].Length)
        {
            caret += Column;
            return caret;
        }
        else
        {
            Debug.Fail("inconsitent position and lines");
            return lines[Row].Length;
        }
    }
}
