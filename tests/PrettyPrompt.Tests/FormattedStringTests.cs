using System.Linq;
using PrettyPrompt.Highlighting;
using Xunit;

namespace PrettyPrompt.Tests
{
    public class FormattedStringTests
    {
        private static readonly ConsoleFormat Red = new(Foreground: AnsiColor.Red);
        private static readonly ConsoleFormat Green = new(Foreground: AnsiColor.Green);
        private static readonly ConsoleFormat Yellow = new(Foreground: AnsiColor.Yellow);

        [Fact]
        public void Concatenation_PreservesFormatting()
        {
            Assert.Equal(
                FormattedString.Empty,
                FormattedString.Empty + FormattedString.Empty);


            var left = new FormattedString("lorem ipsum red sit amet ");
            var right = new FormattedString("lorem green dolor sit amet");

            Assert.Equal(
                new FormattedString("lorem ipsum red sit amet lorem green dolor sit amet"),
                left + right);


            left = new FormattedString("lorem ipsum red sit amet ", new FormatSpan(12, 3, Red));
            right = new FormattedString("lorem green dolor sit amet", new FormatSpan(6, 5, Green));

            Assert.Equal(
                new FormattedString(
                    "lorem ipsum red sit amet lorem green dolor sit amet",
                    new FormatSpan(12, 3, Red),
                    new FormatSpan(31, 5, Green)),
                left + right);
        }

        [Fact]
        public void Trim_PreservesFormatting()
        {
            var value = FormattedString.Empty;
            Assert.Equal(
                FormattedString.Empty,
                value.Trim());


            value = new FormattedString("  lorem ipsum red sit amet     ", new FormatSpan(15, 3, Red));
            Assert.Equal(
                new FormattedString("lorem ipsum red sit amet", new FormatSpan(13, 3, Red)),
                value.Trim());


            value = new FormattedString("     ", new FormatSpan(1, 3, Red));
            Assert.Equal(
                FormattedString.Empty,
                value.Trim());


            value = new FormattedString("red", new FormatSpan(0, 3, Red));
            Assert.Equal(
                value,
                value.Trim());
        }

        [Fact]
        public void Trim_FormatOverTrimmedArea_PreservesFormatting()
        {
            var value = new FormattedString("  lorem     ", new FormatSpan(0, 12, Red));
            Assert.Equal(
                new FormattedString("lorem", new FormatSpan(0, 5, Red)),
                value.Trim());


            value = new FormattedString("  lorem     ", new FormatSpan(0, 1, Red), new FormatSpan(8, 2, Green));
            Assert.Equal(
                "lorem",
                value.Trim());


            value = new FormattedString("  lorem     ", new FormatSpan(0, 3, Red), new FormatSpan(6, 2, Green));
            Assert.Equal(
                new FormattedString("lorem", new FormatSpan(0, 1, Red), new FormatSpan(4, 1, Green)),
                value.Trim());
        }

        [Fact]
        public void Substring_PreservesFormatting()
        {
            var value = FormattedString.Empty;
            Assert.Equal(
                FormattedString.Empty,
                value.Substring(0, 0));


            value = new FormattedString("  lorem ipsum red sit amet     ", new FormatSpan(15, 3, Red));
            Assert.Equal(
                new FormattedString("lorem ipsum red sit amet", new FormatSpan(13, 3, Red)),
                value.Trim());


            value = new FormattedString("     ", new FormatSpan(1, 3, Red));
            Assert.Equal(
                FormattedString.Empty,
                value.Trim());


            value = new FormattedString("red", new FormatSpan(0, 3, Red));
            Assert.Equal(
                value,
                value.Trim());
        }

        [Fact]
        public void Substring_FormatOverTrimmedArea_PreservesFormatting()
        {
            var value = new FormattedString("  lorem     ", new FormatSpan(0, 12, Red));
            Assert.Equal(
                new FormattedString("lorem", new FormatSpan(0, 5, Red)),
                value.Substring(2, 5));


            value = new FormattedString("  lorem     ", new FormatSpan(0, 1, Red), new FormatSpan(8, 2, Green));
            Assert.Equal(
                "lorem",
                value.Substring(2, 5));


            value = new FormattedString("  lorem     ", new FormatSpan(0, 3, Red), new FormatSpan(6, 2, Green));
            Assert.Equal(
                new FormattedString("lorem", new FormatSpan(0, 1, Red), new FormatSpan(4, 1, Green)),
                value.Substring(2, 5));
        }

        [Fact]
        public void ReplaceNonFormatted_PreservesFormatting()
        {
            var value = FormattedString.Empty;
            Assert.Equal(
                FormattedString.Empty,
                value.Replace("ipsum", "XY"));


            value = new FormattedString("lorem ipsum red sit amet", new FormatSpan(12, 3, Red));
            Assert.Equal(
                new FormattedString("lorem XY red sit amet", new FormatSpan(9, 3, Red)),
                value.Replace("ipsum", "XY"));


            value = new FormattedString("lorem ipsumipsum red sit amet", new FormatSpan(17, 3, Red));

            Assert.Equal(
                new FormattedString("lorem XYXY red sit amet", new FormatSpan(11, 3, Red)),
                value.Replace("ipsum", "XY"));

            Assert.Equal(
                new FormattedString("lorem  red sit amet", new FormatSpan(7, 3, Red)),
                value.Replace("ipsum", ""));
        }

        [Fact]
        public void ReplaceFormatted_PreservesFormatting()
        {
            var value = new FormattedString("lorem redredred ipsum", new FormatSpan(6, 3, Red), new FormatSpan(9, 3, Red), new FormatSpan(12, 3, Red));

            Assert.Equal(
                new FormattedString("lorem XYXYXY ipsum", new FormatSpan(6, 2, Red), new FormatSpan(8, 2, Red), new FormatSpan(10, 2, Red)),
                value.Replace("red", "XY"));

            Assert.Equal(
                new FormattedString("lorem  ipsum"),
                value.Replace("red", ""));

            Assert.Equal(
                new FormattedString("lo XY redred ipsum", new FormatSpan(6, 3, Red), new FormatSpan(9, 3, Red)),
                value.Replace("rem red", " XY "));

            Assert.Equal(
                new FormattedString("loredred ipsum", new FormatSpan(2, 3, Red), new FormatSpan(5, 3, Red)),
                value.Replace("rem red", ""));
        }

        [Fact]
        public void Split_PreservesFormatting()
        {
            var value = FormattedString.Empty;
            var parts = value.Split('_').ToArray();
            Assert.Single(parts);
            Assert.Equal(0, parts[0].Length);


            value = new FormattedString("lorem red_ipsum green_dolor sit red", new FormatSpan(6, 3, Red), new FormatSpan(16, 5, Green), new FormatSpan(32, 3, Red));
            parts = value.Split('_').ToArray();
            Assert.Equal(3, parts.Length);
            Assert.Equal(new FormattedString("lorem red", new FormatSpan(6, 3, Red)), parts[0]);
            Assert.Equal(new FormattedString("ipsum green", new FormatSpan(6, 5, Green)), parts[1]);
            Assert.Equal(new FormattedString("dolor sit red", new FormatSpan(10, 3, Red)), parts[2]);
        }

        [Fact]
        public void Split_FormattingOverMultipleParts_PreservesFormatting()
        {
            var value = new FormattedString("lorem r_ee_ddd ipsum", new FormatSpan(6, 8, Red));

            var parts = value.Split('_').ToArray();
            Assert.Equal(3, parts.Length);
            Assert.Equal(new FormattedString("lorem r", new FormatSpan(6, 1, Red)), parts[0]);
            Assert.Equal(new FormattedString("ee", new FormatSpan(0, 2, Red)), parts[1]);
            Assert.Equal(new FormattedString("ddd ipsum", new FormatSpan(0, 3, Red)), parts[2]);
        }

        [Fact]
        public void Split_FormattingOverSeparator_PreservesFormatting()
        {
            var value = new FormattedString("a b", new FormatSpan(1, 1, Red));
            var parts = value.Split(' ').ToArray();
            Assert.Equal(2, parts.Length);
            Assert.Equal("a", parts[0]);
            Assert.Equal("b", parts[1]);


            value = new FormattedString("red yellow", new FormatSpan(0, 4, Red), new FormatSpan(4, 6, Yellow));
            parts = value.Split(' ').ToArray();
            Assert.Equal(2, parts.Length);
            Assert.Equal(new FormattedString("red", new FormatSpan(0, 3, Red)), parts[0]);
            Assert.Equal(new FormattedString("yellow", new FormatSpan(0, 6, Yellow)), parts[1]);


            value = new FormattedString("red (yellow ) green", new FormatSpan(0, 5, Red), new FormatSpan(5, 6, Yellow), new FormatSpan(12, 7, Green));
            parts = value.Split(' ').ToArray();
            Assert.Equal(4, parts.Length);
            Assert.Equal(new FormattedString("red", new FormatSpan(0, 3, Red)), parts[0]);
            Assert.Equal(new FormattedString("(yellow", new FormatSpan(0, 1, Red), new FormatSpan(1, 6, Yellow)), parts[1]);
            Assert.Equal(new FormattedString(")", new FormatSpan(0, 1, Green)), parts[2]);
            Assert.Equal(new FormattedString("green", new FormatSpan(0, 5, Green)), parts[3]);
        }

        [Fact]
        public void SplitIntoChunks_PreservesFormatting()
        {
            var value = new FormattedString("lorem red   ipsum green dolor red", new FormatSpan(6, 3, Red), new FormatSpan(18, 5, Green), new FormatSpan(30, 3, Red));

            var chunks = value.SplitIntoChunks(chunkSize: 12).ToArray();
            Assert.Equal(3, chunks.Length);
            Assert.Equal(new FormattedString("lorem red   ", new FormatSpan(6, 3, Red)), chunks[0]);
            Assert.Equal(new FormattedString("ipsum green ", new FormatSpan(6, 5, Green)), chunks[1]);
            Assert.Equal(new FormattedString("dolor red", new FormatSpan(6, 3, Red)), chunks[2]);
        }

        [Fact]
        public void SplitIntoChunks_FormattingOverMultipleChunks_PreservesFormatting()
        {
            var value = new FormattedString("lorem reeeee ddd sit", new FormatSpan(6, 10, Red));

            var chunks = value.SplitIntoChunks(chunkSize: 7).ToArray();
            Assert.Equal(3, chunks.Length);
            Assert.Equal(new FormattedString("lorem r", new FormatSpan(6, 1, Red)), chunks[0]);
            Assert.Equal(new FormattedString("eeeee d", new FormatSpan(0, 7, Red)), chunks[1]);
            Assert.Equal(new FormattedString("dd sit", new FormatSpan(0, 2, Red)), chunks[2]);
        }

        [Fact]
        public void EnumerateTextElements_PreservesFormatting()
        {
            var value = new FormattedString(
                "abc",
                new FormatSpan(0, 1, Red),
                new FormatSpan(1, 1, Green),
                new FormatSpan(2, 1, Yellow));

            var elements = value.EnumerateTextElements().ToArray();
            Assert.Equal(3, elements.Length);
            Assert.Equal(("a", Red), elements[0]);
            Assert.Equal(("b", Green), elements[1]);
            Assert.Equal(("c", Yellow), elements[2]);


            value = new FormattedString(
                "aaa bb c",
                new FormatSpan(0, 3, Red),
                new FormatSpan(4, 2, Green),
                new FormatSpan(7, 1, Yellow));

            elements = value.EnumerateTextElements().ToArray();
            Assert.Equal(8, elements.Length);
            Assert.Equal(("a", Red), elements[0]);
            Assert.Equal(("a", Red), elements[1]);
            Assert.Equal(("a", Red), elements[2]);
            Assert.Equal((" ", ConsoleFormat.None), elements[3]);
            Assert.Equal(("b", Green), elements[4]);
            Assert.Equal(("b", Green), elements[5]);
            Assert.Equal((" ", ConsoleFormat.None), elements[6]);
            Assert.Equal(("c", Yellow), elements[7]);
        }
    }
}