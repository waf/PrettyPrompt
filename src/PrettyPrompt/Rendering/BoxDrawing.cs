#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

namespace PrettyPrompt.Rendering;

internal static class BoxDrawing
{
    public const char CornerUpperRight = '┐';
    public const char CornerLowerRight = '┘';
    public const char CornerUpperLeft = '┌';
    public const char CornerLowerLeft = '└';
    public const char EdgeHorizontal = '─';
    public const char EdgeVertical = '│';
    public const char EdgeVerticalAndLeftHorizontal = '┤';
    public const char EdgeVerticalAndRightHorizontal = '├';
    public const char EdgeHorizontalAndLowerVertical = '┬';
    public const char EdgeHorizontalAndUpperVertical = '┴';

    public static (string top, string bottom) HorizontalBorders(int width, bool leftCorner = true, bool rightCorner = true)
    {
        var boxHorizontalBorder = new string(EdgeHorizontal, width); // -1 to make up for right corner piece

        return (
            top: (leftCorner ? CornerUpperLeft : "") + boxHorizontalBorder + (rightCorner ? CornerUpperRight : ""),
            bottom: (leftCorner ? CornerLowerLeft : "") + boxHorizontalBorder + (rightCorner ? CornerLowerRight : "")
        );
    }
}
