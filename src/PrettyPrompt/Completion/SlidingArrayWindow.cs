using System;
using System.Collections;
using System.Collections.Generic;

namespace PrettyPrompt.Completion
{
    class SlidingArrayWindow<T> : IEnumerable<T>, IReadOnlyCollection<T>
    {
        private readonly T[] array;
        private readonly int windowSize;
        private int offset;
        private int current;

        public SlidingArrayWindow() : this(Array.Empty<T>(), 0) { }

        public SlidingArrayWindow(T[] array, int windowSize, int current = 0)
        {
            this.array = array;
            this.windowSize = windowSize;
            this.offset = array.Length - windowSize <= 0 
                ? 0
                : Math.Min(current, array.Length - windowSize);
            this.current = current;
        }

        public T SelectedItem => array.Length == 0 ? default : array[current];

        public void IncrementSelectedIndex()
        {
            if (current == array.Length - 1)
                return;

            current++;

            if(current >= offset + windowSize && offset + windowSize < array.Length)
            {
                offset++;
            }
        }

        public void DecrementSelectedIndex()
        {
            if (current == 0)
                return;

            current--;

            if(current < offset && offset > 0)
            {
                offset--;
            }
        }

        public void ResetSelectedIndex()
        {
            current = 0;
            offset = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return AsArraySegment().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return AsArraySegment().GetEnumerator();
        }

        private ArraySegment<T> AsArraySegment()
        {
            return new ArraySegment<T>(array, offset, Math.Min(windowSize, array.Length));
        }

        public int Count => array.Length;
    }
}
