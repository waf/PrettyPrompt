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
    }
}
