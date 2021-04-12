using System;
using System.Collections.Generic;

namespace PrettyPrompt.Highlighting
{
    public class ConsoleFormat : IEquatable<ConsoleFormat>
    {
        public ConsoleFormat(AnsiColor foreground = null, AnsiColor background = null, bool bold = false, bool underline = false)
        {
            Foreground = foreground;
            Background = background;
            Bold = bold;
            Underline = underline;
        }

        public AnsiColor Foreground { get; }
        public AnsiColor Background { get; }
        public bool Bold { get; }
        public bool Underline { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as ConsoleFormat);
        }

        public bool Equals(ConsoleFormat other)
        {
            return other != null &&
                   EqualityComparer<AnsiColor>.Default.Equals(Foreground, other.Foreground) &&
                   EqualityComparer<AnsiColor>.Default.Equals(Background, other.Background) &&
                   Bold == other.Bold &&
                   Underline == other.Underline;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Foreground, Background, Bold, Underline);
        }

        public static bool operator ==(ConsoleFormat left, ConsoleFormat right)
        {
            return EqualityComparer<ConsoleFormat>.Default.Equals(left, right);
        }

        public static bool operator !=(ConsoleFormat left, ConsoleFormat right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return (
                (Foreground is null ? "" : $"Foreground: {Foreground} ")
                + (Background is null ? "" : $"Background: {Background} ")
            ).Trim();
        }
    }
}
