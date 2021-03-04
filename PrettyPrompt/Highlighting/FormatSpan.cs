namespace PrettyPrompt.Highlighting
{
    public class FormatSpan
    {
        public FormatSpan(int start, int length, ConsoleFormat formatting)
        {
            Start = start;
            Length = length;
            Formatting = formatting;
        }

        public int Start { get; set; }
        public int Length { get; set; }
        public ConsoleFormat Formatting { get; }
    }
}
