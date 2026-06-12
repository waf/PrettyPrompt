#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Linq;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;
using Xunit;

namespace PrettyPrompt.Tests;

/// <summary>
/// Tests for the junction characters <see cref="BoxDrawing.Connect"/> draws between the overload box
/// (signature help, stacked above) and the completion + documentation boxes (side by side, below).
/// Boxes are built the way the renderer builds them: content width n produces box width n + 4
/// (border + padding on each side), and line count n produces n + 2 rows (top/bottom borders).
/// </summary>
public class BoxDrawingTests
{
    private readonly PromptConfiguration configuration = new();
    private readonly BoxDrawing boxDrawing;

    public BoxDrawingTests()
    {
        boxDrawing = new BoxDrawing(configuration);
    }

    [Fact]
    public void Connect_OverloadAndEqualWidthCompletion_NoDocumentation_DrawsStackedJunctions()
    {
        // Regression test: this exact shape crashed with ArgumentOutOfRangeException — the no-documentation
        // branch replaced at index documentationBoxWidth - 1 == -1 instead of completionBoxWidth - 1. Hit in
        // practice when no documentation pane is visible (e.g. inspect mode, where the target's assemblies
        // have no XML docs) and the completion box width drifts into exact equality with the overload box.
        var overload = Lines(contentWidth: 8, lineCount: 1);
        var completion = Items(contentWidth: 8, itemCount: 1);

        boxDrawing.Connect(overload, completion, Array.Empty<Row>());

        Assert.Equal("├──────────┤", RowText(completion[0]));
    }

    [Fact]
    public void Connect_OverloadWiderThanCompletion_NoDocumentation()
    {
        var overload = Lines(contentWidth: 10, lineCount: 1);
        var completion = Items(contentWidth: 8, itemCount: 1);

        boxDrawing.Connect(overload, completion, Array.Empty<Row>());

        Assert.Equal("├──────────┬", RowText(completion[0]));
    }

    [Fact]
    public void Connect_OverloadNarrowerThanCompletion_NoDocumentation()
    {
        var overload = Lines(contentWidth: 4, lineCount: 1);
        var completion = Items(contentWidth: 8, itemCount: 1);

        boxDrawing.Connect(overload, completion, Array.Empty<Row>());

        Assert.Equal("├──────┴───┐", RowText(completion[0]));
    }

    [Fact]
    public void Connect_OverloadSpansCompletionPlusDocumentation()
    {
        // overload box (24) == completion box (12) + documentation box (12); equal heights.
        var overload = Lines(contentWidth: 20, lineCount: 1);
        var completion = Items(contentWidth: 8, itemCount: 1);
        var documentation = Lines(contentWidth: 8, lineCount: 1);

        boxDrawing.Connect(overload, completion, documentation);

        Assert.Equal("├──────────┐", RowText(completion[0]));
        Assert.Equal("┬──────────┤", RowText(documentation[0]));
        Assert.Equal("┴──────────┘", RowText(documentation[^1]));
    }

    [Fact]
    public void Connect_OverloadEqualsCompletionWidth_WithDocumentation_DrawsCross()
    {
        var overload = Lines(contentWidth: 8, lineCount: 1);
        var completion = Items(contentWidth: 8, itemCount: 1);
        var documentation = Lines(contentWidth: 8, lineCount: 1);

        boxDrawing.Connect(overload, completion, documentation);

        Assert.Equal("├──────────┐", RowText(completion[0]));
        Assert.Equal("┼──────────┐", RowText(documentation[0]));
    }

    [Fact]
    public void Connect_OverloadBetweenCompletionAndTotalWidth_WithDocumentation()
    {
        // completion box (12) < overload box (16) < completion + documentation (24).
        var overload = Lines(contentWidth: 12, lineCount: 1);
        var completion = Items(contentWidth: 8, itemCount: 1);
        var documentation = Lines(contentWidth: 8, lineCount: 1);

        boxDrawing.Connect(overload, completion, documentation);

        Assert.Equal("├──────────┐", RowText(completion[0]));
        Assert.Equal("┬───┴──────┐", RowText(documentation[0]));
    }

    [Fact]
    public void Connect_OverloadWiderThanCompletionPlusDocumentation()
    {
        var overload = Lines(contentWidth: 24, lineCount: 1);
        var completion = Items(contentWidth: 8, itemCount: 1);
        var documentation = Lines(contentWidth: 8, lineCount: 1);

        boxDrawing.Connect(overload, completion, documentation);

        Assert.Equal("├──────────┐", RowText(completion[0]));
        Assert.Equal("┬──────────┬", RowText(documentation[0]));
    }

    [Fact]
    public void Connect_DocumentationShorterThanCompletion_NoOverload()
    {
        var completion = Items(contentWidth: 8, itemCount: 3);
        var documentation = Lines(contentWidth: 8, lineCount: 1);

        boxDrawing.Connect(Array.Empty<Row>(), completion, documentation);

        Assert.Equal("┬──────────┐", RowText(documentation[0]));
        Assert.Equal("├──────────┘", RowText(documentation[^1]));
        Assert.Equal("┌──────────┐", RowText(completion[0])); // untouched without an overload box
    }

    [Fact]
    public void Connect_DocumentationTallerThanCompletion_NoOverload()
    {
        var completion = Items(contentWidth: 8, itemCount: 1);
        var documentation = Lines(contentWidth: 8, lineCount: 3);

        boxDrawing.Connect(Array.Empty<Row>(), completion, documentation);

        Assert.Equal("┬──────────┐", RowText(documentation[0]));
        // the ┤ lands on the documentation row aligned with the completion box's bottom border.
        Assert.Equal('┤', RowText(documentation[completion.Length - 1])[0]);
    }

    [Fact]
    public void Connect_EmptyCompletionBox_DoesNothing()
    {
        var overload = Lines(contentWidth: 8, lineCount: 1);

        boxDrawing.Connect(overload, Array.Empty<Row>(), Array.Empty<Row>());

        Assert.Equal("┌──────────┐", RowText(overload[0])); // untouched
    }

    private Row[] Lines(int contentWidth, int lineCount) =>
        boxDrawing.BuildFromLines(
            Enumerable.Range(0, lineCount).Select(_ => (FormattedString)new string('a', contentWidth)),
            configuration,
            background: null);

    private Row[] Items(int contentWidth, int itemCount) =>
        boxDrawing.BuildFromItemList(
            Enumerable.Range(0, itemCount).Select(_ => (FormattedString)new string('a', contentWidth)),
            configuration,
            maxWidth: int.MaxValue);

    private static string RowText(Row row) =>
        string.Concat(Enumerable.Range(0, row.Length).Select(i => row[i].Text));
}
