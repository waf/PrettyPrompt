using System;

namespace PrettyPrompt
{
    public static class Extensions
    {

        public static string EnvironmentNewlines(this string text)
        {
            string environmentNewline = Environment.NewLine;
            if (environmentNewline == "\n")
                return text;
            else
                return text.Replace("\n", environmentNewline);
        }
    }
}
