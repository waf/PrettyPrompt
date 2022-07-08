#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Diagnostics;
using PrettyPrompt.Rendering;

namespace PrettyPrompt;

internal sealed class StringCache
{
    private readonly (string Text, byte UnicodeWidth)[] asciiCache = new (string, byte)[256];

    public static readonly StringCache Shared = new();

    private StringCache()
    {
        for (int i = 0; i < asciiCache.Length; i++)
        {
            var c = (char)i;
            asciiCache[i] = (c.ToString(), checked((byte)UnicodeWidth.GetWidth(c)));
        }
    }

    public string Get(char character, out int unicodeWidth)
    {
        var idx = (int)character;
        if (idx < asciiCache.Length)
        {
            (var text, unicodeWidth) = asciiCache[idx];
            return text;
        }
        else
        {
            var result = character.ToString();
            Debug.Assert(result.Length == 1);
            unicodeWidth = UnicodeWidth.GetWidth(character);
            return result;
        }
    }

    public string Get(ReadOnlySpan<char> characters, out int unicodeWidth)
    {
        if (characters.Length == 1)
        {
            return Get(characters[0], out unicodeWidth);
        }
        else
        {
            var result = characters.ToString();
            unicodeWidth = UnicodeWidth.GetWidth(result);
            return result;
        }
    }
}