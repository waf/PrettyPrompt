using System;

namespace PrettyPrompt
{
    public static class Extensions
    {
        public static string EnvironmentNewlines(this string text) =>
            Environment.NewLine == "\n"
                ? text
                : text.Replace("\n", Environment.NewLine);
    }
}
