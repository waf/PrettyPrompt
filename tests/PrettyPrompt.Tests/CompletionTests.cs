#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrettyPrompt.Consoles;
using Xunit;
using static System.ConsoleKey;
using static System.ConsoleModifiers;
using static System.Environment;

namespace PrettyPrompt.Tests;

public class CompletionTests
{
    [Fact]
    public async Task ReadLine_SingleCompletion()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Aa{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Aardvark", result.Text);
    }

    [Fact]
    public async Task ReadLine_MultipleCompletion()
    {
        var console = ConsoleStub.NewConsole();
        // complete 3 animals. For the third animal, start completing Alligator, but then backspace, navigate the completion menu and complete as Albatross instead.
        console.StubInput($"Aa{Enter} Z{Tab} Alli{Backspace}{Backspace}{DownArrow}{UpArrow}{DownArrow}{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Aardvark Zebra Albatross", result.Text);
    }

    [Fact]
    public async Task ReadLine_MultilineCompletion()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Aa{Enter}{Shift}{Enter}Z{Control}{Spacebar}{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark{NewLine}Zebra", result.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionMenu_AutoOpens()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"A{Enter}{Shift}{Enter}Z{Enter}{Enter}");

        Prompt prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Ant{NewLine}Zebra", result.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionMenu_Closes()
    {
        var console = ConsoleStub.NewConsole();
        Prompt prompt = ConfigurePrompt(console);

        // Escape should close menu
        console.StubInput($"A{Escape}{Enter}"); // it will auto-open when we press A (see previous test)
        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"A", result.Text);

        // Home key (among others) should close menu
        console.StubInput($"A{Home}{Enter}");
        var result2 = await prompt.ReadLineAsync();

        Assert.True(result2.IsSuccess);
        Assert.Equal($"A", result2.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionMenu_Scrolls()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"{Control}{Spacebar}{Control}{Spacebar}",
            $"{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}",
            $"{Enter}{Enter}"
        );
        Prompt prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Zebra", result.Text);

        console.StubInput(
            $"{Control}{Spacebar}{Control}{Spacebar}",
            $"{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}{DownArrow}",
            $"{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}{UpArrow}",
            $"{Enter}{Enter}"
        );

        var result2 = await prompt.ReadLineAsync();
        Assert.True(result2.IsSuccess);
        Assert.Equal($"Aardvark", result2.Text);
    }

    [Fact]
    public async Task ReadLine_FullyTypeCompletion_CanOpenAgain()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"Aardvark {Control}{Spacebar}{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark Aardvark", result.Text);
    }

    [Fact]
    public async Task ReadLine_EmptyPrompt_AutoOpens()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"{Control}{Spacebar}{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Aardvark", result.Text);
    }

    [Fact]
    public async Task ReadLine_OpenWindowAtBeginningOfPrompt_Opens()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"a{LeftArrow} {LeftArrow}a{Enter}{Enter}");

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Ant a", result.Text);
    }

    [Fact]
    public async Task ReadLine_CompletionWithNoMatches_DoesNotAutoComplete()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"A{Enter} Q{Enter}"); // first {Enter} selects an autocompletion, second {Enter} submits because there are no completions.

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal($"Ant Q", result.Text);
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/22.
    /// </summary>
    [Fact]
    public async Task ReadLine_AddNewLines_ReturnToStart_ShowCompletionList_ConfirmInput_ShouldRedraw()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"{Shift}{Enter}{Shift}{Enter}{Shift}{Enter}{Shift}{Enter}", //new lines
            $"{Control}{Home}", //return to start
            $"{Control}{Spacebar}", //show completion list
            $"{Enter}"); //confirm input

        var prompt = ConfigurePrompt(console);

        var result = await prompt.ReadLineAsync();

        Assert.True(result.IsSuccess);

        var output = console.GetAllOutput();
        var indexOfOutputWithCompletionPane = output.Select((o, i) => (o, i)).First(t => t.o.Contains("┌───────────")).i;
        Assert.Contains("            ", output[indexOfOutputWithCompletionPane + 1]); //completion pane should be cleared
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/65.
    /// </summary>
    [Fact]
    public async Task ReadLine_CompletionInsertionNotFromEndOfWord()
    {
        const string Text = "abcde";
        for (int insertCaretPosition = 0; insertCaretPosition < Text.Length; insertCaretPosition++)
        {
            var console = ConsoleStub.NewConsole();

            var input = new List<FormattableString>() { $"{Text}" };
            input.AddRange(Enumerable.Repeat<FormattableString>($"{LeftArrow}", count: Text.Length - insertCaretPosition));
            input.Add($"{Control}{Spacebar}"); //show completion list
            input.Add($"{Enter}{Enter}"); //insert completion and submit prompt

            console.StubInput(input.ToArray());
            var prompt = ConfigurePrompt(console, completions: new[] { Text.ToUpper() });
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(Text.ToUpper(), result.Text);
        }
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/95.
    /// </summary>
    [Fact]
    public async Task ReadLine_WriteWordNotInCompletionList_TriggerCompletionList_ShouldNotWriteSpace()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"abc",
            $"{Escape}", //close completion list
            $"{Control}{Spacebar}", //trigger new one
            $"{Escape}", //close completion list
            $"{Enter}");
        var prompt = ConfigurePrompt(console, completions: new[] { "aaa" });
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abc", result.Text);
    }

    /// <summary>
    /// Test behaviour described in https://github.com/waf/PrettyPrompt/issues/67.
    /// </summary>
    [Fact]
    public async Task ReadLine_CaretMovingInsideWord_WhileCompletionListIsOpen_WontCloseTheList()
    {
        const string TextPrefix = "abc ";
        const string TextSuffix = " ijk";
        const string MainWord = "defgh";
        const string Text = $"{TextPrefix}{MainWord}{TextSuffix}";
        //leftmost and rightmost caret positions: "abc |defgh| ijk"
        for (int caretPosition = TextPrefix.Length; caretPosition <= TextPrefix.Length + MainWord.Length; caretPosition++)
        {
            var console = ConsoleStub.NewConsole();

            var input = new List<FormattableString>() { $"{Text}" };
            input.AddRange(Enumerable.Repeat<FormattableString>($"{LeftArrow}", count: Text.Length - caretPosition)); //setup starting caret position
            input.Add($"{Control}{Spacebar}"); //show completion list
            input.AddRange(Enumerable.Repeat<FormattableString>($"{LeftArrow}", count: caretPosition - TextPrefix.Length)); //move with caret to the left word border
            input.AddRange(Enumerable.Repeat<FormattableString>($"{RightArrow}", count: caretPosition - TextPrefix.Length)); //move with caret to the right word border
            input.Add($"{Enter}{Enter}"); //insert completion and submit prompt

            console.StubInput(input.ToArray());
            var prompt = ConfigurePrompt(console, completions: new[] { MainWord.ToUpper() });
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("abc DEFGH ijk", result.Text);
        }
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/96.
    /// </summary>
    [Fact]
    public async Task ReadLine_StartWritingWord_NonWordCharacterShouldCloseCompletion()
    {
        var spacebarIsNotCommitCharacterCfg = new PromptConfiguration(
            keyBindings: new KeyBindings(
                commitCompletion: new[] { new KeyPressPattern(Enter) }));

        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"ab",
            $"{Spacebar}", //this should close completion list (without insertion) and write space
            $"{Enter}");
        var prompt = ConfigurePrompt(console, completions: new[] { "abcd" }, configuration: spacebarIsNotCommitCharacterCfg);
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ab ", result.Text);

        //------------------------------------------

        var spacebarIsCommitCharacterCfg = new PromptConfiguration(
         keyBindings: new KeyBindings(
             commitCompletion: new[] { new KeyPressPattern(Spacebar) }));

        console = ConsoleStub.NewConsole();
        console.StubInput(
            $"ab",
            $"{Spacebar}", //this should insert completion item and write space
            $"{Enter}");
        prompt = ConfigurePrompt(console, completions: new[] { "abcd" }, configuration: spacebarIsCommitCharacterCfg);
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("abcd ", result.Text);
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/99.
    /// </summary>
    [Fact]
    public async Task ReadLine_CommitCompletionItemByCharacter_ShouldInsertCompletion_And_InsertPressedCharacter()
    {
        foreach (var commitChar in new[] { ' ', '.', '(' })
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"ab",
                $"{commitChar}", //should insert completion
                $"{Escape}", //to be sure that following Enter won't insert completion
                $"{Enter}"); //submit prompt
            var prompt = ConfigurePrompt(
                console,
                completions: new[] { "abcd" },
                configuration: new PromptConfiguration(
                    keyBindings: new KeyBindings(
                        commitCompletion: new KeyPressPatterns(
                            new(Enter), new(Tab), new(' '), new('.'), new('(')))
                    ));
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal($"abcd{commitChar}", result.Text);
        }

        foreach (var commitKey in new[] { Tab, Enter })
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"ab",
                $"{commitKey}", //should insert completion
                $"{Escape}", //to be sure that following Enter won't insert completion
                $"{Enter}"); //submit prompt
            var prompt = ConfigurePrompt(
                console,
                completions: new[] { "abcd" },
                configuration: new PromptConfiguration(
                    keyBindings: new KeyBindings(
                        commitCompletion: new KeyPressPatterns(
                            new(Enter), new(Tab), new(' '), new('.'), new('(')))
                    ));
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal($"abcd", result.Text);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/100 was causing this test to fail.
    /// </summary>
    [Fact]
    public async Task ReadLine_GetFilteredCompletionListOnSecondWord_SelectAll_WriteLetter_CompletionListShouldBeRefreshed()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"aaa b",
            $"{Escape}{Control}{Spacebar}", //to be sure completion list is shown
            $"{Control}{A}", //select all
            $"a",
            $"{Enter}", //insert completion
            $"{Enter}"); //submit prompt
        var prompt = ConfigurePrompt(
            console,
            completions: new[] { "aaa", "bbb" });
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal($"aaa", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/126 was causing this test to fail.
    /// </summary>
    [Fact]
    public async Task ReadLine_WriteWord_WriteDot_RightSelectDotAndLastLetter_TriggerSelection_ShouldNotOpenCompletionList()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"aaa.",
            $"{LeftArrow}{LeftArrow}{Shift}{RightArrow}{Shift}{RightArrow}", //right select 'a.'
            $"{Control}{Spacebar}", //try to show completion list (should not open)
            $"{Enter}"); //submit prompt
        var prompt = ConfigurePrompt(
            console,
            completions: new[] { "aaa", "bbb" });
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal($"aaa.", result.Text);
    }

    [Fact]
    public async Task ReadLine_TriggerCompletionListInWord_PressHomeOrEndToGetToAnotherWord_ListShouldClose()
    {
        foreach (var homeOrEnd in new[] { Home, End })
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput(
                $"abc defg hij",
                $"{LeftArrow}{LeftArrow}{LeftArrow}{LeftArrow}{LeftArrow}{LeftArrow}", // move caret -> "abc de|fg hij"
                $"{Escape}", //close completion list
                $"{Control}{Spacebar}", //show completion list
                $"{homeOrEnd}", //should close completion list
                $"{Enter}"); //submit prompt
            var prompt = ConfigurePrompt(console, completions: new[] { "abc", "defg", "hij" });
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("abc defg hij", result.Text);
        }
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/102.
    /// </summary>
    [Fact]
    public async Task ReadLine_CompletionList_AutoReopen_After_InsertionWithNonControlCharacter()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"a",
            $".", //insert completion - completion list should immediately reopen after
            $"{DownArrow}", //select next completion item
            $"{Enter}", //insert completion
            $"{Enter}"); //submit prompt
        var prompt = ConfigurePrompt(
            console,
            completions: new[] { "aaa", "bbb" },
            configuration: new PromptConfiguration(
            keyBindings: new KeyBindings(
                commitCompletion: new KeyPressPatterns(
                    new(Enter), new(Tab), new(' '), new('.'), new('(')))
            ));
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal($"aaa.bbb", result.Text);
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/105.
    /// </summary>
    [Fact]
    public async Task ReadLine_CompletionList_InsertWithNonControlCharacter_Backspace()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput(
            $"DateTime",
            $".", //insert completion - completion list should immediately reopen after
            $"{Backspace}.", //delete dot and enter it again which should open comletion list again
            $"{DownArrow}{Enter}", //insert second completion
            $"{Enter}"); //submit prompt
        var prompt = ConfigurePrompt(
            console,
            completions: new[] { "Now" },
            configuration: new PromptConfiguration(
            keyBindings: new KeyBindings(
                commitCompletion: new KeyPressPatterns(
                    new(Enter), new(Tab), new(' '), new('.'), new('(')))
            ));
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal($"DateTime.Now", result.Text);
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/112.
    /// </summary>
    [Fact]
    public async Task ReadLine_CompletionItemPriority()
    {
        string Input = "stringbuilder";
        for (int i = 1; i <= 6; i++)
        {
            await Test(Input.Substring(0, i), "string");
        }

        for (int i = 7; i <= Input.Length; i++)
        {
            await Test(Input.Substring(0, i), "StringBuilder");
        }

        Input = "builder";
        for (int i = 1; i <= Input.Length; i++)
        {
            await Test(Input.Substring(0, i), "StringBuilder");
        }

        static async Task Test(string input, string expectedOutput)
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"{input}{Enter}{Enter}");
            var prompt = ConfigurePrompt(console, completions: new[] { "int", "string", "StringBuilder" });
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedOutput, result.Text);
        }
    }

    /// <summary>
    /// Tests bug from https://github.com/waf/PrettyPrompt/issues/113.
    /// </summary>
    [Fact]
    public async Task ReadLine_NonMatchingCompletionItem_IsNotInserted()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"ab{Enter}{Enter}");
        var prompt = ConfigurePrompt(
            console,
            completions: new[] { "aaa" },
            configuration: new PromptConfiguration(
            keyBindings: new KeyBindings(
                commitCompletion: new KeyPressPatterns(
                    new(Enter), new('.')))
            ));
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ab", result.Text);

        ////////////////////////////////////////////////////

        console = ConsoleStub.NewConsole();
        console.StubInput($"ab.{Escape}{Enter}"); //dot inserts completion; Esc closes newly open list
        prompt = ConfigurePrompt(
           console,
           completions: new[] { "aaa" },
           configuration: new PromptConfiguration(
           keyBindings: new KeyBindings(
               commitCompletion: new KeyPressPatterns(
                   new(Enter), new('.')))
           ));
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("ab.", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/114.
    /// </summary>
    [Fact]
    public async Task ReadLine_NonMatchingPattern()
    {
        foreach (var arrow in new[] { DownArrow, UpArrow })
        {
            var console = ConsoleStub.NewConsole();
            console.StubInput($"a{arrow}{Enter}");
            var prompt = ConfigurePrompt(
                console,
                completions: new[] { "111", "222", "333" });
            var result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("111", result.Text);

            //////////////////////////////////////////////////////

            console = ConsoleStub.NewConsole();
            console.StubInput($"a{arrow}{DownArrow}{Enter}");
            prompt = ConfigurePrompt(
               console,
               completions: new[] { "111", "222", "333" });
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("222", result.Text);

            //////////////////////////////////////////////////////

            console = ConsoleStub.NewConsole();
            console.StubInput($"a{arrow}{UpArrow}{Enter}");
            prompt = ConfigurePrompt(
               console,
               completions: new[] { "111", "222", "333" });
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("111", result.Text);

            //////////////////////////////////////////////////////

            console = ConsoleStub.NewConsole();
            console.StubInput($"a{arrow}b{Enter}");
            prompt = ConfigurePrompt(
               console,
               completions: new[] { "111", "222", "333" });
            result = await prompt.ReadLineAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal("ab", result.Text);
        }
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/114.
    /// </summary>
    [Fact]
    public async Task ReadLine_NonMatchingPattern_ReselectAfterDeleteOfNonMatchingPart()
    {
        var console = ConsoleStub.NewConsole();
        console.StubInput($"1a{Backspace}{Enter}{Enter}");
        var prompt = ConfigurePrompt(
            console,
            completions: new[] { "111", "222", "333" });
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("111", result.Text);

        //////////////////////////////////////////////////////

        console = ConsoleStub.NewConsole();
        console.StubInput($"a1{LeftArrow}{LeftArrow}{Delete}{DownArrow}{Enter}{Enter}");
        prompt = ConfigurePrompt(
            console,
            completions: new[] { "111", "222", "333" });
        result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("222", result.Text);
    }

    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/147.
    /// </summary>
    [Fact]
    public async Task ReadLine_TriggerCompletionWhenIsAlreadyOpen_And_CommitCharacterColidesWithThriggerCharacter()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = ConfigurePrompt(
            console,
            completions: new[] { "aaa" },
            configuration: new PromptConfiguration(
                keyBindings: new KeyBindings(
                    commitCompletion: new(new KeyPressPattern(' ')),
                    triggerCompletionList: new(new KeyPressPattern(Control, Spacebar))
                    )));
        console.StubInput(
            $"a", //should open completion list
            $"{Control}{Spacebar}", //trigger should do nothing (in #147 it commited completion item)
            $"{Escape}", //close completion list
            $"{Enter}"); //submit prompt
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Text);
    }


    /// <summary>
    /// https://github.com/waf/PrettyPrompt/issues/185
    /// </summary>
    [Fact]
    public async Task TransformKeyPressAsync_ShouldNotBeCalledAlways()
    {
        var console = ConsoleStub.NewConsole();
        var prompt = ConfigurePrompt(
            console,
            configuration: new PromptConfiguration(
                keyBindings: new KeyBindings(
                    commitCompletion: new[] { new KeyPressPattern(Enter) },
                    submitPrompt: new[] { new KeyPressPattern(Control, Enter) },
                    newLine: new[] { new KeyPressPattern(Enter) }
                    )),
            completions: new[] { "aaa" });
        console.StubInput(
            $"a", //should open completion list
            $"{Enter}", //should insert item (bug was that new line was inserted instead)
            $"{Control}{Enter}"); //submit prompt
        var result = await prompt.ReadLineAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("aaa", result.Text);
    }

    public static Prompt ConfigurePrompt(IConsole console, PromptConfiguration? configuration = null, string[]? completions = null) =>
        new(
            callbacks: new TestPromptCallbacks
            {
                CompletionCallback = new CompletionTestData(completions).CompletionHandlerAsync
            },
            console: console,
            configuration: configuration
        );
}