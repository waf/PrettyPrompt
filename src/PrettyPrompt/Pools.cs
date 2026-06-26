#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Concurrent;
using System.Threading;

namespace PrettyPrompt;

/// <summary>
/// Base for our small allocation-saving object pools. Lock-free, modelled on the techniques in ASP.NET Core's
/// <c>DefaultObjectPool&lt;T&gt;</c> (we deliberately don't take a dependency on Microsoft.Extensions.ObjectPool,
/// just borrow the approach): a single <c>fastItem</c> hot slot, claimed/released with an
/// <see cref="Interlocked"/> compare-exchange, backed by a lock-free <see cref="ConcurrentQueue{T}"/> for overflow.
///
/// Retention is bounded by <c>maxRetained</c> so a burst of returns can't grow the pool without limit; returns
/// beyond the cap are dropped and left to the GC. Pass <see cref="int.MaxValue"/> for an effectively unbounded
/// pool (which also skips the counter bookkeeping) when the working set is large but naturally bounded by usage -
/// e.g. the per-frame cells/rows, where alternating rent/return on the render thread self-limits the pool size.
/// </summary>
internal abstract class LockFreePool<T> where T : class
{
    private readonly bool bounded;
    private readonly int maxRetained;
    private int numRetained;
    private readonly ConcurrentQueue<T> overflow = new();
    private T? fastItem;

    protected LockFreePool(int maxRetained)
    {
        bounded = maxRetained != int.MaxValue;
        // reserve one logical slot for fastItem, mirroring DefaultObjectPool.
        this.maxRetained = bounded ? maxRetained - 1 : int.MaxValue;
    }

    /// <summary>Takes a pooled instance, or null when the pool is empty (the caller then creates one).</summary>
    protected T? Rent()
    {
        // fast path: the single hot slot, claimed atomically.
        var item = fastItem;
        if (item is not null && Interlocked.CompareExchange(ref fastItem, null, item) == item)
        {
            return item;
        }
        // slow path: the overflow queue.
        if (overflow.TryDequeue(out item))
        {
            if (bounded) Interlocked.Decrement(ref numRetained);
            return item;
        }
        return null;
    }

    /// <summary>Returns an instance to the pool. Callers reset it (e.g. Clear) before returning.</summary>
    protected void ReturnToPool(T item)
    {
        // fast path: drop it into the hot slot if it's free.
        if (fastItem is null && Interlocked.CompareExchange(ref fastItem, item, null) is null)
        {
            return;
        }
        // slow path: enqueue, unless we're already at the retention cap (then drop it for the GC).
        if (!bounded)
        {
            overflow.Enqueue(item);
        }
        else if (Interlocked.Increment(ref numRetained) <= maxRetained)
        {
            overflow.Enqueue(item);
        }
        else
        {
            Interlocked.Decrement(ref numRetained);
        }
    }
}

internal sealed class ListPool<T> : LockFreePool<List<T>>
{
    public static readonly ListPool<T> Shared = new();

    // Per-frame Rows each hold a pooled list; the working set is large and the render thread alternates
    // rent/return, so keep this unbounded rather than risk thrashing with a too-small cap.
    private ListPool() : base(maxRetained: int.MaxValue) { }

    public List<T> Get(int capacity)
    {
        var list = Rent();
        if (list is null)
        {
            return new List<T>(capacity);
        }
        if (list.Capacity < capacity)
        {
            list.Capacity = capacity;
        }
        return list;
    }

    public void Put(List<T> list)
    {
        list.Clear();
        ReturnToPool(list);
    }
}

internal sealed class StringBuilderPool : LockFreePool<StringBuilder>
{
    public static readonly StringBuilderPool Shared = new();

    // Only a couple are ever in flight at once (the word-wrap working buffer and the diff buffer).
    private StringBuilderPool() : base(maxRetained: 16) { }

    public StringBuilder Get(int capacity)
    {
        var sb = Rent();
        if (sb is null)
        {
            return new StringBuilder(capacity);
        }
        if (sb.Capacity < capacity)
        {
            sb.Capacity = capacity;
        }
        return sb;
    }

    public void Put(StringBuilder builder)
    {
        builder.Clear();
        ReturnToPool(builder);
    }
}

/// <summary>
/// Pools the <see cref="Cell"/>?[] backing buffer of a <see cref="Rendering.Screen"/>. The renderer keeps the
/// current and previous screen alive, so two buffers ping-pong; a small cap covers that plus a little slack
/// across a resize (when the buffer size changes and stale-sized buffers are discarded). Buffers are sized
/// exactly to <c>Width*Height</c>, so callers can keep relying on <c>CellBuffer.Length</c>.
/// </summary>
internal sealed class ScreenBufferPool : LockFreePool<Cell?[]>
{
    public static readonly ScreenBufferPool Shared = new();

    private ScreenBufferPool() : base(maxRetained: 8) { }

    public Cell?[] Get(int length)
    {
        if (length == 0)
        {
            return Array.Empty<Cell?>();
        }
        var buffer = Rent();
        // Pooled buffers are cleared on return; a wrong-sized one (left over from before a resize) is dropped
        // here (not re-pooled) and replaced, so the pool converges back to the current size.
        return buffer is not null && buffer.Length == length ? buffer : new Cell?[length];
    }

    public void Put(Cell?[] buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }
        // the buffer holds references to cells that are about to be recycled, so null them out before reuse.
        Array.Clear(buffer, 0, buffer.Length);
        ReturnToPool(buffer);
    }
}
