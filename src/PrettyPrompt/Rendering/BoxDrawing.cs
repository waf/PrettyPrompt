#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Rendering;

internal static class BoxDrawing
{
    //
    // ATTENTION: When changing collection of following characters don't forget to update MinUsedCharacterValue/MaxUsedCharacterValue constants.
    //            Also make sure their difference is sufficiently small because we allocate cache of that size.
    //
    public const char CornerUpperRight = '┐';
    public const char CornerLowerRight = '┘';
    public const char CornerUpperLeft = '┌';
    public const char CornerLowerLeft = '└';
    public const char EdgeHorizontal = '─';
    public const char EdgeVertical = '│';
    public const char EdgeVerticalAndLeftHorizontal = '┤';
    public const char EdgeVerticalAndRightHorizontal = '├';
    public const char EdgeHorizontalAndLowerVertical = '┬';
    public const char EdgeHorizontalAndUpperVertical = '┴';

    private const string CornerUpperRightText = "┐";
    private const string CornerLowerRightText = "┘";
    private const string CornerUpperLeftText = "┌";
    private const string CornerLowerLeftText = "└";
    private const string EdgeHorizontalText = "─";
    private const string EdgeVerticalText = "│";
    //private const string EdgeVerticalAndLeftHorizontalText = "┤";
    //private const string EdgeVerticalAndRightHorizontalText = "├";
    //private const string EdgeHorizontalAndLowerVerticalText = "┬";
    //private const string EdgeHorizontalAndUpperVerticalText = "┴";

    public const int MinUsedCharacterValue = EdgeHorizontal;
    public const int MaxUsedCharacterValue = EdgeHorizontalAndUpperVertical;

    public static Row[] BuildFromItemList(
        IEnumerable<FormattedString> items,
        PromptConfiguration configuration,
        int maxWidth,
        int? selectedLineIndex = null)
    {
        return BuildInternal(
            items,
            configuration,
            maxWidth,
            selectedLineIndex,
            configuration.SelectedCompletionItemMarker,
            configuration.UnselectedCompletionItemMarker,
            background: null);
    }

    public static Row[] BuildFromLines(
        IEnumerable<FormattedString> lines,
        PromptConfiguration configuration,
        in AnsiColor? background)
    {
        return BuildInternal(
            lines,
            configuration,
            maxWidth: int.MaxValue,
            selectedLineIndex: null,
            selectedLineMarker: " ",
            unselectedLineMarker: " ",
            background);
    }

    private static Row[] BuildInternal(
        IEnumerable<FormattedString> lines,
        PromptConfiguration configuration,
        int maxWidth,
        int? selectedLineIndex,
        FormattedString selectedLineMarker,
        string unselectedLineMarker,
        in AnsiColor? background)
    {
        const string Padding = " ";
        int lineMarkerWidth = UnicodeWidth.GetWidth(unselectedLineMarker);
        var leftPaddingWidth = selectedLineIndex.HasValue ? lineMarkerWidth : Padding.Length;
        if (maxWidth </*leftBorder*/EdgeVerticalText.Length + leftPaddingWidth + /*rightPadding*/Padding.Length + /*leftBorder*/EdgeVerticalText.Length)
        {
            return Array.Empty<Row>();
        }

        if (!lines.TryGetNonEnumeratedCount(out var capacity)) capacity = 16;

        var lineList = ListPool<FormattedString>.Shared.Get(capacity);
        int maxLineWidth = 0;
        foreach (var line in lines)
        {
            lineList.Add(line);
            var lineWidth = line.GetUnicodeWidth();
            if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
        }

        var boxWidth = Math.Min(
            /*leftBorder*/EdgeVerticalText.Length + leftPaddingWidth + maxLineWidth + /*rightPadding*/Padding.Length + /*rightBorder*/EdgeVerticalText.Length,
            maxWidth);

        var rows = ListPool<Row>.Shared.Get(lineList.Count);
        ref readonly var completionBoxBorderFormat = ref configuration.CompletionBoxBorderFormat;

        //Top border.
        var row = new Row(boxWidth);
        row.Add(CornerUpperLeftText, completionBoxBorderFormat);
        for (int i = CornerUpperLeftText.Length + CornerUpperRightText.Length; i < boxWidth; i++) row.Add(EdgeHorizontalText, completionBoxBorderFormat);
        row.Add(CornerUpperRightText, completionBoxBorderFormat);
        rows.Add(row);

        //Lines.
        var lineAvailableWidth = boxWidth -/*leftBorder*/EdgeVerticalText.Length - leftPaddingWidth - /*rightPadding*/Padding.Length - /*rightBorder*/EdgeVerticalText.Length;
        for (int i = 0; i < lineList.Count; i++)
        {
            row = new Row(boxWidth);
            var line = lineList[i];
            FillLineRow(row, line.Substring(0, Math.Min(line.Length, lineAvailableWidth)), i, completionBoxBorderFormat, background);
            rows.Add(row);
        }

        //Bottom border.
        row = new Row(boxWidth);
        row.Add(CornerLowerLeftText, completionBoxBorderFormat);
        for (int i = CornerUpperLeftText.Length + CornerUpperRightText.Length; i < boxWidth; i++) row.Add(EdgeHorizontalText, completionBoxBorderFormat);
        row.Add(CornerLowerRightText, completionBoxBorderFormat);
        rows.Add(row);

        var result = rows.ToArray();
        ListPool<Row>.Shared.Put(rows);
        ListPool<FormattedString>.Shared.Put(lineList);
        return result;

        void FillLineRow(Row row, FormattedString line, int lineIndex, in ConsoleFormat completionBoxBorderFormat, in AnsiColor? background)
        {
            //Left border.
            row.Add(EdgeVerticalText, completionBoxBorderFormat);

            //Left padding.
            bool isSelected = false;
            if (selectedLineIndex.TryGet(out var selectedLineIndexValue))
            {
                if (selectedLineIndexValue == lineIndex)
                {
                    isSelected = true;
                    row.Add(selectedLineMarker);
                }
                else
                {
                    row.Add(unselectedLineMarker);
                }
            }
            else
            {
                row.Add(Padding);
            }

            //Line.
            row.Add(line);

            //Right padding.
            var rightPaddingWidth = maxLineWidth - line.GetUnicodeWidth() + 1;
            for (int i = 0; i < rightPaddingWidth; i++)
            {
                row.Add(Padding);
            }

            if (background != null)
            {
                row.TransformBackground(
                    background,
                    startIndex: EdgeVerticalText.Length + lineMarkerWidth,
                    count: row.Length - EdgeVerticalText.Length - lineMarkerWidth - 1);
            }

            if (isSelected)
            {
                row.TransformBackground(
                    configuration.SelectedCompletionItemBackground,
                    startIndex: EdgeVerticalText.Length + lineMarkerWidth,
                    count: row.Length - EdgeVerticalText.Length - lineMarkerWidth - 1);
            }

            //Right border.
            row.Add(EdgeVerticalText, completionBoxBorderFormat);
        }
    }
}