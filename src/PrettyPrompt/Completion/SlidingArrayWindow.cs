#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections;
using System.Collections.Generic;

namespace PrettyPrompt.Completion
{
    /// <summary>
    /// Datastructure that provides a window over a segment of an array, similar to <see cref="ArraySegment{T}"/>, but
    /// also has a concept of the window "sliding" to always keep a selected index in view. This datastructure powers
    /// the auto-complete menu, and the window slides to provide the scrolling of the menu.
    /// </summary>
    sealed class SlidingArrayWindow<T> : IReadOnlyCollection<T>
    {
        private readonly T[] array;
        private readonly int windowLength;
        private readonly int windowBuffer;
        private int windowStart;
        private int selectedIndex;

        public SlidingArrayWindow() : this(Array.Empty<T>(), 0) { }

        public SlidingArrayWindow(T[] array, int windowLength = 10, int selectedIndex = 0, int windowBuffer = 3)
        {
            this.array = array;
            this.windowLength = windowLength;
            this.windowStart = CalculateWindowStart(array, windowLength, selectedIndex);
            this.selectedIndex = selectedIndex;
            this.windowBuffer = windowBuffer;
        }

        public T SelectedItem =>
            array.Length == 0 ? default : array[selectedIndex];

        public void IncrementSelectedIndex()
        {
            if (selectedIndex == array.Length - 1)
                return;

            selectedIndex++;

            if(selectedIndex + windowBuffer >= windowStart + windowLength && windowStart + windowLength < array.Length)
            {
                windowStart++;
            }
        }

        public void DecrementSelectedIndex()
        {
            if (selectedIndex == 0)
                return;

            selectedIndex--;

            if(selectedIndex - windowBuffer < windowStart && windowStart > 0)
            {
                windowStart--;
            }
        }

        public void ResetSelectedIndex()
        {
            selectedIndex = 0;
            windowStart = 0;
        }

        private static int CalculateWindowStart(T[] array, int windowLength, int selectedIndex) =>
            array.Length - windowLength <= 0
            ? 0
            : Math.Min(selectedIndex, array.Length - windowLength);

        public IEnumerator<T> GetEnumerator() =>
            AsArraySegment().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            AsArraySegment().GetEnumerator();

        private ArraySegment<T> AsArraySegment() =>
            new ArraySegment<T>(array, windowStart, Math.Min(windowLength, array.Length));

        public int Count => array.Length;
    }
}
