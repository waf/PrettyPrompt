#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System.Collections.Generic;
using System.Linq;
using PrettyPrompt.Documents;
using Xunit;

namespace PrettyPrompt.Tests;

public class DocumentTests
{
    [Fact]
    public void WordBoundariesTests_WithoutWhitspaces()
    {
        //empty
        var document = new Document("", caret: 0);
        Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(1));
        Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));


        //single char
        var testChars = new[] { 'a', '(' };
        foreach (var c in testChars)
        {
            document = new Document($"{c}", caret: 0);
            for (int i = 0; i <= 1; i++)
            {
                document.Caret = i;
                Assert.Equal(1, document.CalculateWordBoundaryIndexNearCaret(1));
                Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));
            }
        }


        //two chars
        IEnumerable<string> GetTestPairs(bool distinctChars)
        {
            var testPairs =
                from c1 in testChars
                from c2 in testChars
                select (c1, c2);
            return testPairs.Where(p => distinctChars ? p.c1 != p.c2 : p.c1 == p.c2).Select(p => $"{p.c1}{p.c2}");
        }
        foreach (var text in GetTestPairs(distinctChars: true))
        {
            document = new Document(text, caret: 0);

            document.Caret = 0;
            Assert.Equal(1, document.CalculateWordBoundaryIndexNearCaret(1));
            Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));

            document.Caret = 1;
            Assert.Equal(2, document.CalculateWordBoundaryIndexNearCaret(1));
            Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));

            document.Caret = 2;
            Assert.Equal(2, document.CalculateWordBoundaryIndexNearCaret(1));
            Assert.Equal(1, document.CalculateWordBoundaryIndexNearCaret(-1));
        }
        foreach (var text in GetTestPairs(distinctChars: false))
        {
            document = new Document(text, caret: 0);
            for (int i = 0; i >= 2; i++)
            {
                document.Caret = i;
                Assert.Equal(2, document.CalculateWordBoundaryIndexNearCaret(1));
                Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));
            }
        }


        //positions:             012345678
        document = new Document(")))aaa(((", caret: 0);

        //move right
        for (int i = 0; i <= 2; i++)
        {
            document.Caret = i;
            Assert.Equal(3, document.CalculateWordBoundaryIndexNearCaret(1));
        }

        for (int i = 3; i <= 5; i++)
        {
            document.Caret = i;
            Assert.Equal(6, document.CalculateWordBoundaryIndexNearCaret(1));
        }

        for (int i = 6; i <= document.Length; i++)
        {
            document.Caret = i;
            Assert.Equal(9, document.CalculateWordBoundaryIndexNearCaret(1));
        }


        //move left
        for (int i = document.Length; i >= 7; i--)
        {
            document.Caret = i;
            Assert.Equal(6, document.CalculateWordBoundaryIndexNearCaret(-1));
        }

        for (int i = 6; i >= 4; i--)
        {
            document.Caret = i;
            Assert.Equal(3, document.CalculateWordBoundaryIndexNearCaret(-1));
        }

        for (int i = 3; i >= 0; i--)
        {
            document.Caret = i;
            Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));
        }
    }

    [Fact]
    public void WordBoundariesTests_WithWhitspaces()
    {
        //Different editors have different behaviour regarding to whitespaces.
        //Have tried Visual Studio vs Visual Studio Code vs Windows Terminal.
        //VS behaves as Terminal. VsCode behaves diffrently. So VS/Terminal behaviour is implemented.

        //whitespaces only
        var document = new Document("   ", caret: 0);
        for (int i = 0; i <= document.Length; i++)
        {
            document.Caret = i;
            Assert.Equal(document.Length, document.CalculateWordBoundaryIndexNearCaret(1));
            Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));
        }

        //word + whitespaces (behaves like single word when going right;  behaves like two word when going left)
        document = new Document("abc   ", caret: 0);
        for (int i = 0; i <= document.Length; i++)
        {
            document.Caret = i;
            Assert.Equal(document.Length, document.CalculateWordBoundaryIndexNearCaret(1));
            Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));
        }

        //whitespaces + word (behaves like two words)
        document = new Document("   abc", caret: 0);
        for (int i = 0; i <= 2; i++)
        {
            document.Caret = i;
            Assert.Equal(3, document.CalculateWordBoundaryIndexNearCaret(1));
        }
        for (int i = 3; i <= document.Length; i++)
        {
            document.Caret = i;
            Assert.Equal(document.Length, document.CalculateWordBoundaryIndexNearCaret(1));
        }
        for (int i = 0; i <= 3; i++)
        {
            document.Caret = i;
            Assert.Equal(0, document.CalculateWordBoundaryIndexNearCaret(-1));
        }
        for (int i = 4; i <= document.Length; i++)
        {
            document.Caret = i;
            Assert.Equal(3, document.CalculateWordBoundaryIndexNearCaret(-1));
        }
    }

    [Fact]
    public void MoveToLineBoundary_SmartHomeTest()
    {
        var document = new Document("    abcd", caret: 0);
        for (int i = 0; i <= 3; i++)
        {
            document.Caret = i;
            document.MoveToLineBoundary(-1);
            Assert.Equal(4, document.Caret);
        }

        document.Caret = 4;
        document.MoveToLineBoundary(-1);
        Assert.Equal(0, document.Caret);

        for (int i = 5; i < document.Length; i++)
        {
            document.Caret = i;
            document.MoveToLineBoundary(-1);
            Assert.Equal(4, document.Caret);
        }
    }
}
