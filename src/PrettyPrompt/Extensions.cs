#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrettyPrompt
{
    internal static class Extensions
    {
        public static string EnvironmentNewlines(this string text) =>
            Environment.NewLine == "\n"
                ? text
                : text.Replace("\n", Environment.NewLine);

        public static IEnumerable<string> EnumerateTextElements(this string text)
        {
            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while(enumerator.MoveNext())
            {
                yield return enumerator.GetTextElement();
            }
        }

        public static IEnumerable<string> SplitIntoSubstrings(this string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }
    }
}
