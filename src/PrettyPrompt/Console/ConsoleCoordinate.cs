namespace PrettyPrompt.Consoles
{
    internal class ConsoleCoordinate
    {
        public ConsoleCoordinate(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; set; }
        public int Column { get; set; }
    }
}
