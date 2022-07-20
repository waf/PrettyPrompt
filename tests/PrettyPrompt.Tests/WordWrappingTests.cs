using System.Linq;
using System.Text;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using Xunit;

namespace PrettyPrompt.Tests;

public class WordWrappingTests
{
    [Fact]
    public void WrapEditableCharacters_GivenLongText_WrapsCharacters()
    {
        var text = "Here is some text that should be wrapped character by character";
        var wrapped = WordWrapping.WrapEditableCharacters(new StringBuilder(text), text.Length - 5, 20);

        Assert.Equal(
            new[]
            {
                new WrappedLine(0,  "Here is some text th"),
                new WrappedLine(20, "at should be wrapped"),
                new WrappedLine(40, " character by charac"),
                new WrappedLine(60, "ter"),
            },
            wrapped.WrappedLines
        );
        Assert.Equal(new ConsoleCoordinate(2, 18), wrapped.Cursor);
    }

    [Fact]
    public void WrapEditableCharacters_DoubleWidthCharacters_UsesStringWidth()
    {
        var text = "每个人都有他的作战策略，直到脸上中了一拳。";
        var wrapped = WordWrapping.WrapEditableCharacters(new StringBuilder(text), caret: 13, width: 20);

        Assert.Equal(
            new[]
            {
                new WrappedLine(0, "每个人都有他的作战策"),
                new WrappedLine(10, "略，直到脸上中了一拳"),
                new WrappedLine(20, "。"),
            },
            wrapped.WrappedLines
        );
        Assert.Equal(new ConsoleCoordinate(1, 3), wrapped.Cursor);
    }


    [Fact]
    public void WrapEditableCharacters_DoubleWidthCharactersWithWrappingInMiddleOfCharacter_WrapsCharacter()
    {
        var text = "每个人都有他的作战策略， 直到脸上中了一拳。";
        var wrapped = WordWrapping.WrapEditableCharacters(new StringBuilder(text), caret: 19, width: 19);

        Assert.Equal(
            new[]
            {
                new WrappedLine(0, "每个人都有他的作战"),  // case 1: we should wrap early, because the next character is a full-width (2-wide) character.
                new WrappedLine(9, "策略， 直到脸上中了"), // case 2: single width space ("normal" space) sets us to align to width 19 exactly.
                new WrappedLine(19, "一拳。")
            },
            wrapped.WrappedLines
        );

        Assert.Equal(new ConsoleCoordinate(2, 0), wrapped.Cursor);

    }

    [Fact]
    public void WrapWords_GivenLongText_WrapsWords()
    {
        var text = "Here is some text that should be wrapped word by word. supercalifragilisticexpialidocious";
        var wrapped = WordWrapping.WrapWords(text, 20).Select(l => l.Text);

        Assert.Equal(
            new[]
            {
                "Here is some text",
                "that should be",
                "wrapped word by",
                "word.",
                "supercalifragilistic",
                "expialidocious",
            },
            wrapped
        );
    }

    [Fact]
    public void WrapWords_WithNewLines_SplitsAtNewLines()
    {
        var text = "Here is some\ntext that should be wrapped word by\nword. supercalifragilisticexpialidocious";
        
        var wrapped = WordWrapping.WrapWords(text, 20).Select(l => l.Text);
        Assert.Equal(
            new[]
            {
                "Here is some",
                "text that should be",
                "wrapped word by",
                "word.",
                "supercalifragilistic",
                "expialidocious",
            },
            wrapped
        );

        wrapped = WordWrapping.WrapWords(text, 20, maxLines: 5).Select(l => l.Text);
        Assert.Equal(
            new[]
            {
                "Here is some",
                "text that should be",
                "wrapped word by",
                "word.",
                "supercalifragilis...",
            },
            wrapped
        );
    }

    [Fact]
    public void WrapWords_DoubleWidthCharacters_UsesUnicodeWidth()
    {
        var text = "每个人都有他的作战策略，直到脸上中了一拳。";
        var wrapped = WordWrapping.WrapWords(text, 20).Select(l => l.Text);

        Assert.Equal(
            new[]
            {
                "每个人都有他的作战策",
                "略，直到脸上中了一拳",
                "。",
            },
            wrapped
        );
    }

    [Fact]
    public void WrapWords_MultipleNewlines()
    {
        var text = "Here is some text that should be wrapped word by word.\n\nHERE IS SOME TEXT THAT SHOULD BE WRAPPED WORD BY WORD.";

        var wrapped = WordWrapping.WrapWords(text, 20).Select(l => l.Text);
        Assert.Equal(
            new[]
            {
                "Here is some text",
                "that should be",
                "wrapped word by",
                "word.",
                "",
                "HERE IS SOME TEXT",
                "THAT SHOULD BE",
                "WRAPPED WORD BY",
                "WORD.",
            },
            wrapped
        );

        wrapped = WordWrapping.WrapWords(text, 20, maxLines: 1).Select(l => l.Text);
        Assert.Equal(
            new[]
            {
                "Here is some text...",
            },
            wrapped
        );

        wrapped = WordWrapping.WrapWords(text, 20, maxLines: 2).Select(l => l.Text);
        Assert.Equal(
        new[]
            {
                "Here is some text",
                "that should be...",
            },
            wrapped
        );

        wrapped = WordWrapping.WrapWords(text, 20, maxLines: 3).Select(l => l.Text);
        Assert.Equal(
        new[]
            {
                "Here is some text",
                "that should be",
                "wrapped word by...",
            },
            wrapped
        );
    }

    [Fact]
    public void WrapWords_LengthEqualsToWrapLength()
    {
        var text = "Here is some teeeext"; //Length = 20
        var wrapped = WordWrapping.WrapWords(text, 20).Select(l => l.Text);
        Assert.Equal(new[] { "Here is some teeeext" }, wrapped);

        wrapped = WordWrapping.WrapWords(text, 20, maxLines: 1).Select(l => l.Text);
        Assert.Equal(new[] { "Here is some teeeext" }, wrapped);
    }

    [Fact]
    public void WrapWords_Empty()
    {
        var wrapped = WordWrapping.WrapWords("", 20).Select(l => l.Text);
        Assert.Equal(new string[] { }, wrapped);
        
        wrapped = WordWrapping.WrapWords("", 20, maxLines: 1).Select(l => l.Text);
        Assert.Equal(new string[] { }, wrapped);
    }

    [Fact]
    public void WrapWords_NewLineAtStart()
    {
        var wrapped = WordWrapping.WrapWords("\nhello", 20).Select(l => l.Text);
        Assert.Equal(new string[] { "", "hello" }, wrapped);

        wrapped = WordWrapping.WrapWords("\nhello", 20, maxLines: 1).Select(l => l.Text);
        Assert.Equal(new string[] { "..." }, wrapped);

        wrapped = WordWrapping.WrapWords("\nhello", 20, maxLines: 2).Select(l => l.Text);
        Assert.Equal(new string[] { "", "hello" }, wrapped);
    }
}
