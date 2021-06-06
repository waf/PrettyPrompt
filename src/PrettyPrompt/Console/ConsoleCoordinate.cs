#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;

namespace PrettyPrompt.Consoles
{
    internal sealed class ConsoleCoordinate : IEquatable<ConsoleCoordinate>
    {
        public ConsoleCoordinate(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; set; }
        public int Column { get; set; }

        public override bool Equals(object obj) =>
            Equals(obj as ConsoleCoordinate);

        public bool Equals(ConsoleCoordinate other) =>
            other != null
            && Row == other.Row
            && Column == other.Column;

        public override int GetHashCode() =>
            HashCode.Combine(Row, Column);
    }
}
