#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.TextSelection;

namespace PrettyPrompt.Highlighting;

/// <summary>
/// Given the text and the syntax highlighting information, render the text into the "cells" of the terminal screen.
/// </summary>
internal static class CellRenderer
{
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, IReadOnlyList<WrappedLine> lines, SelectionSpan? selection, AnsiColor? selectedTextBackground)
    {
        var selectionStart = new ConsoleCoordinate(int.MaxValue, int.MaxValue); //invalid
        var selectionEnd = new ConsoleCoordinate(int.MaxValue, int.MaxValue); //invalid
        if (selection.TryGet(out var selectionValue))
        {
            selectionStart = selectionValue.Start;
            selectionEnd = selectionValue.End;
        }

        bool selectionHighlight = false;

        var highlightsLookup = highlights
            .ToLookup(h => h.Start)
            .ToDictionary(h => h.Key, conflictingHighlights => conflictingHighlights.OrderByDescending(h => h.Length).First());
        var highlightedRows = new Row[lines.Count];
        FormatSpan? currentHighlight = null;
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            WrappedLine line = lines[lineIndex];
            int lineFullWidthCharacterOffset = 0;
            var row =new Row(line.Content);
            for (int cellIndex = 0; cellIndex < row.Length; cellIndex++)
            {
                var cell = row[cellIndex];
                if (cell.IsContinuationOfPreviousCharacter)
                    lineFullWidthCharacterOffset++;

                // syntax highlight wrapped lines
                if (currentHighlight.TryGet(out var previousLineHighlight) &&
                    cellIndex == 0)
                {
                    currentHighlight = HighlightSpan(previousLineHighlight, row, cellIndex, previousLineHighlight.Start - line.StartIndex);
                }

                // get current syntaxt highlight start
                int characterPosition = line.StartIndex + cellIndex - lineFullWidthCharacterOffset;
                currentHighlight ??= highlightsLookup.TryGetValue(characterPosition, out var lookupHighlight) ? lookupHighlight : null;

                // syntax highlight based on start
                if (currentHighlight.TryGet(out var highlight) &&
                    highlight.Contains(characterPosition))
                {
                    currentHighlight = HighlightSpan(highlight, row, cellIndex, cellIndex);
                }

                // if there's text selected, invert colors to represent the highlight of the selected text.
                if (selectionStart.Equals(lineIndex, cellIndex - lineFullWidthCharacterOffset)) //start is inclusive
                {
                    selectionHighlight = true;
                }
                if (selectionEnd.Equals(lineIndex, cellIndex - lineFullWidthCharacterOffset)) //end is exclusive
                {
                    selectionHighlight = false;
                }
                if (selectionHighlight)
                {
                    if (selectedTextBackground.TryGet(out var background))
                    {
                        cell.TransformBackground(background);
                    }
                    else
                    {
                        cell.Formatting = new ConsoleFormat { Inverted = true };
                    }
                }
            }
            highlightedRows[lineIndex] = row;
        }
        return highlightedRows;
    }

    private static FormatSpan? HighlightSpan(FormatSpan currentHighlight, Row row, int cellIndex, int endPosition)
    {
        var highlightedFullWidthOffset = 0;
        int i;
        for (i = cellIndex; i < Math.Min(endPosition + currentHighlight.Length + highlightedFullWidthOffset, row.Length); i++)
        {
            highlightedFullWidthOffset += row[i].ElementWidth - 1;
            row[i].Formatting = currentHighlight.Formatting;
        }
        if (i != row.Length)
        {
            return null;
        }

        return currentHighlight;
    }

    /// <summary>
    /// This is just an extra function used by <see cref="Prompt.RenderAnsiOutput"/> that highlights arbitrary text. It's
    /// not used for drawing input during normal functioning of the prompt.
    /// </summary>
    public static Row[] ApplyColorToCharacters(IReadOnlyCollection<FormatSpan> highlights, string text, int textWidth)
    {
        var wrapped = WordWrapping.WrapEditableCharacters(new StringBuilder(text), 0, textWidth);
        return ApplyColorToCharacters(highlights, wrapped.WrappedLines, selection: null, selectedTextBackground: null);
    }
}
