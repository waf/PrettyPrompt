#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Text;

namespace PrettyPrompt;

internal sealed class ListPool<T>
{
    private readonly Stack<List<T>> pool = new();

    public static readonly ListPool<T> Shared = new();

    public List<T> Get(int capacity)
    {
        List<T>? result = null;
        lock (pool)
        {
            if (pool.Count > 0)
            {
                result = pool.Pop();
            }
        }
        if (result is null)
        {
            result = new List<T>(capacity);
        }
        else
        {
            if (result.Capacity < capacity)
            {
                result.Capacity = capacity;
            }
        }
        return result;
    }

    public void Put(List<T> list)
    {
        list.Clear();
        lock (pool)
        {
            pool.Push(list);
        }
    }
}

internal sealed class StringBuilderPool
{
    private readonly Stack<StringBuilder> pool = new();

    public static readonly StringBuilderPool Shared = new();

    public StringBuilder Get(int capacity)
    {
        StringBuilder? result = null;
        lock (pool)
        {
            if (pool.Count > 0)
            {
                result = pool.Pop();
            }
        }
        if (result is null)
        {
            result = new StringBuilder(capacity);
        }
        else
        {
            if (result.Capacity < capacity)
            {
                result.Capacity = capacity;
            }
        }
        return result;
    }

    public void Put(StringBuilder list)
    {
        list.Clear();
        lock (pool)
        {
            pool.Push(list);
        }
    }
}