#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using PrettyPrompt.Consoles;

namespace PrettyPrompt;

/// <summary>
/// An area of the screen that's being rendered at a coordinate.
/// This is conceptually a UI pane, rasterized into characters.
/// </summary>
internal sealed record ScreenArea(ConsoleCoordinate Start, Row[] Rows, bool TruncateToScreenHeight = true, int ViewPortStart = 0) : IDisposable
{
    public static readonly ScreenArea Empty = new(ConsoleCoordinate.Zero, Array.Empty<Row>());

    public int Width => Rows.Length > 0 ? Rows[0].Length : 0;

    public void Dispose()
    {
        foreach (var row in Rows)
        {
            row.Dispose();
        }
#if DEBUG
        Array.Clear(Rows, 0, Rows.Length);
#endif
    }
}