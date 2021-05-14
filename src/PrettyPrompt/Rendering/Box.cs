using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrettyPrompt.Rendering
{
    class Box
    {
        public const char CornerUpperRight = '┐';
        public const char CornerLowerRight = '┘';
        public const char CornerUpperLeft = '┌';
        public const char CornerLowerLeft = '└';
        public const char EdgeHorizontal = '─';
        public const char EdgeVertical = '│';

        public static (string top, string bottom) HorizontalBorders(int width, bool leftCorner = true, bool rightCorner = true)
        {
            var boxHorizontalBorder = new string(EdgeHorizontal, width); // -1 to make up for right corner piece

            return (
                top: (leftCorner ? CornerUpperLeft : "") + boxHorizontalBorder + (rightCorner ? CornerUpperRight : ""),
                bottom: (leftCorner ? CornerLowerLeft : "") + boxHorizontalBorder + (rightCorner ? CornerLowerRight : "")
            );
        }
    }
}
