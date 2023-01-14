using System;
using System.Text;
using PrettyPrompt.Completion;
using Xunit;

namespace PrettyPrompt.Tests;

//Modified tests from: https://github.com/Turnerj/Quickenshtein/blob/main/tests/Quickenshtein.Tests/LevenshteinTestBase.cs

public class LevenshteinDistanceTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("test", "test")]
    [InlineData("abcdefghijklmnopqrstuvwxyz", "abcdefghijklmnopqrstuvwxyz")]
    public void ZeroDistance(string source, string target)
    {
        var distance = CompletionItem.LevenshteinDistance(source, target);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void ZeroDistance_128_Characters()
    {
        var value = BuildString("abcd", 128);
        var distance = CompletionItem.LevenshteinDistance(value, value);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void ZeroDistance_512_Characters()
    {
        var value = BuildString("abcd", 512);
        var distance = CompletionItem.LevenshteinDistance(value, value);
        Assert.Equal(0, distance);
    }

    [Theory]
    [InlineData("He1lo Wor1d", "Hello World", 2)]
    [InlineData("Hello World", "He1lo Wor1d", 2)]
    [InlineData("He1lo Wor1d", "Hell0 World", 3)]
    [InlineData("Hell0 World", "He1lo Wor1d", 3)]
    public void SplitDifference(string source, string target, int expectedDistance)
    {
        var distance = CompletionItem.LevenshteinDistance(source, target);
        Assert.Equal(expectedDistance, distance);
    }

    [Theory]
    [InlineData("", "abcdef", 6)]
    [InlineData("abcdef", "", 6)]
    [InlineData("abcdef", "zyxwvu", 6)]
    public void CompletelyDifferent(string source, string target, int expectedDistance)
    {
        var distance = CompletionItem.LevenshteinDistance(source, target);
        Assert.Equal(expectedDistance, distance);
    }

    [Fact]
    public void CompletelyDifferent_128_Characters()
    {
        var firstArg = BuildString("abcd", 128);
        var secondArg = BuildString("wxyz", 128);
        var distance = CompletionItem.LevenshteinDistance(firstArg, secondArg);
        Assert.Equal(128, distance);
    }

    [Fact]
    public void CompletelyDifferent_512_Characters()
    {
        var firstArg = BuildString("abcd", 512);
        var secondArg = BuildString("wxyz", 512);
        var distance = CompletionItem.LevenshteinDistance(firstArg, secondArg);
        Assert.Equal(512, distance);
    }

    [Fact]
    public void IsCaseSensitive()
    {
        const double LetterCaseChange = 0.1;
        var distance = CompletionItem.LevenshteinDistance("Hello World", "hello world", LetterCaseChange);
        Assert.Equal(2 * LetterCaseChange, distance);
    }

    [Theory]
    [InlineData("Hello World", "Hello World!", 1)]
    [InlineData("Hello World, this is a string.", "Hello World.", 18)]
    [InlineData("Hello World!", "Hello World", 1)]
    [InlineData("Hello World.", "Hello World, this is a string.", 18)]
    public void Addition(string source, string target, int expectedDistance)
    {
        var distance = CompletionItem.LevenshteinDistance(source, target);
        Assert.Equal(expectedDistance, distance);
    }

    [Theory]
    [InlineData("ello World", "Hello World", 1)]
    [InlineData("Hello Worl", "Hello World", 1)]
    [InlineData("Hello World", "ello World", 1)]
    [InlineData("Hello World", "Hello Worl", 1)]
    [InlineData("Hell World", "Hello World", 1)]
    [InlineData("Hello World", "Hell World", 1)]
    public void Deletion(string source, string target, int expectedDistance)
    {
        var distance = CompletionItem.LevenshteinDistance(source, target);
        Assert.Equal(expectedDistance, distance);
    }

    [Fact]
    public void EdgeDifference()
    {
        var distance = CompletionItem.LevenshteinDistance(
            $"a{BuildString("b", 256 + 7)}c",
            $"y{BuildString("b", 256 + 7)}z"
        );
        Assert.Equal(2, distance);
    }

    [Theory]
    [InlineData("bbbbbbbbbbbbbbbbbbbbbbbba", "bbbbbbbbbbbbbbbbbbbbbbbbz")]
    [InlineData("abbbbbbbbbbbbbbbbbbbbbbbb", "zbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void Trim(string source, string target)
    {
        var distance = CompletionItem.LevenshteinDistance(source, target);
        Assert.Equal(1, distance);
    }

    [Theory]
    [InlineData("yorwyeawgn", "xcodeuwtnx", 8)]
    [InlineData("yorwyeagwn", "xcodeuwtnx", 9)]
    [InlineData("yorwyeagnw", "xcodeuwtnx", 9)]
    [InlineData("yorwyaegnw", "xcodeuwtnx", 9)]
    [InlineData("yorwyeawgnb", "xcodeuwtnx", 8)]
    [InlineData("byorwyeawgn", "xcodeuwtnx", 8)]
    [InlineData("yorwyeawgn", "xcodeuwtnxb", 9)]
    [InlineData("yorwyeawgn", "bxcodeuwtnx", 8)]
    [InlineData("yorwyeagwnb", "xcodeuwtnx", 9)]
    [InlineData("yorwyeagnwb", "xcodeuwtnx", 10)]
    [InlineData("yorwyaegnwb", "xcodeuwtnx", 10)]
    [InlineData("abdegjklsjgofmdlacmpdv", "adbegjlkjsogmdaalcmpvd", 10)]
    [InlineData("aaaabbbbccccddddeeee", "aaababbcbccdcddedeee", 5)]
    [InlineData("xkzQJEnvucuhXyKYGqtY", "YTZkcmyTyrvuhDLmswfM", 18.1)]
    [InlineData("BDLZfcIOFxTwWBdGzZxp", "kDiHMMYqOMHkMTByTuGu", 18)]
    [InlineData("cBFZNfiKhzCtgbyoxqMP", "wwyUZFQsRbyUcozbPrtR", 19.1)]
    [InlineData("aaaabbbbccffccddddeeee", "aaababbcbcfcdcddedeee", 7)]
    [InlineData("aaaabbbbccfccddddeeee", "aaababbcbcffcdcddedeee", 6)]
    [InlineData("Seven", " of Nine", 7)]
    public void MiscDistances(string source, string target, double expectedDistance)
    {
        const double LetterCaseChange = 0.1;
        var distance = CompletionItem.LevenshteinDistance(source, target, LetterCaseChange);
        Assert.Equal(expectedDistance, distance, tolerance: 0.001);
    }

    private static string BuildString(string baseString, int numberOfCharacters)
    {
        var builder = new StringBuilder(numberOfCharacters);
        var charBlocksRemaining = (int)Math.Floor((double)numberOfCharacters / baseString.Length);

        while (charBlocksRemaining > 0)
        {
            charBlocksRemaining--;
            builder.Append(baseString);
        }

        var remainder = numberOfCharacters % baseString.Length;
        if (remainder > 0)
        {
            builder.Append(baseString.Substring(0, remainder));
        }

        return builder.ToString();
    }
}