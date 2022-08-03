#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;
using PrettyPrompt.Rendering;
using static System.ConsoleKey;

namespace PrettyPrompt.Panes;

internal class OverloadPane : IKeyPressHandler
{
    private const int SharedLinesWithCompletionPane = 1;
    public static readonly int MinHeight = GetHeight(1, 1, 1);

    private static int GetHeight(int signatureDedicatedLines, int summaryDedicatedLines, int parameterDescriptionDedicatedLines)
        => BoxDrawing.VerticalBordersHeight + signatureDedicatedLines + summaryDedicatedLines + parameterDescriptionDedicatedLines - SharedLinesWithCompletionPane;

    private readonly CodePane codePane;
    private readonly IPromptCallbacks promptCallbacks;
    private readonly PromptConfiguration configuration;

    private object? patternToProcessInKeyUp;

    public IReadOnlyList<OverloadItem> Items { get; private set; } = Array.Empty<OverloadItem>();

    public int Width { get; private set; }

    private int selectedItemIndex;
    private int SelectedItemIndex
    {
        get => selectedItemIndex;
        set
        {
            if (value < 0) value += Items.Count;
            else if (value >= Items.Count) value -= Items.Count;
            selectedItemIndex = value;
            UpdateSelectedItem();
        }
    }

    private int selectedArgumentIndex;

    public IReadOnlyList<FormattedString> SelectedItem { get; private set; } = Array.Empty<FormattedString>();

    /// <summary>
    /// Whether or not the window is currently open / visible.
    /// </summary>
    public bool IsOpen { get; set; }

    public OverloadPane(
        CodePane codePane,
        IPromptCallbacks promptCallbacks,
        PromptConfiguration configuration)
    {
        this.codePane = codePane;
        this.promptCallbacks = promptCallbacks;
        this.configuration = configuration;
    }

    private async Task<bool> TryOpen(int preferedSelectedIndex, CancellationToken cancellationToken)
    {
        (var items, selectedArgumentIndex) = await promptCallbacks.GetOverloadsAsync(codePane.Document.GetText(), codePane.Document.Caret, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0) return false;

        if (codePane.CodeAreaHeight - codePane.Cursor.Row >= /*cursor*/1 + MinHeight)
        {
            Items = items;
            IsOpen = true;
            SelectedItemIndex = preferedSelectedIndex < Items.Count ? preferedSelectedIndex : 0;
            return true;
        }
        return false;
    }

    private void Close()
    {
        IsOpen = false;
        Width = 0;
        Items = Array.Empty<OverloadItem>();
    }

    public int GetCurrentHeight() => IsOpen ? SelectedItem.Count : 0;

    private void UpdateSelectedItem()
    {
        if (Items.Count == 0)
        {
            SelectedItem = Array.Empty<FormattedString>();
            return;
        }

        var selectedOverload = Items[SelectedItemIndex];
        var counter =
            Items.Count > 1 ?
            $"{SelectedItemIndex + 1}/{Items.Count} " :
            "";

        var boxDrawingHorizontalBordersWidth = BoxDrawing.GetHorizontalBordersWidth(BoxType.TextLines, configuration);
        var availableWidth = codePane.CodeAreaWidth - boxDrawingHorizontalBordersWidth - counter.Length;
        var availableHeight = codePane.EmptySpaceAtBottomOfWindowHeight;
        if (counter.Length >= availableWidth)
        {
            SelectedItem = Array.Empty<FormattedString>();
            return;
        }

        var spacesUnderCounter = new string(' ', counter.Length);

        var sb = new FormattedStringBuilder();
        Span<int> dedicatedLines = stackalloc int[] { int.MaxValue, int.MaxValue, int.MaxValue };
        List<FormattedString> signatureLines;
        List<FormattedString> summaryLines;
        IReadOnlyList<FormattedString> paramDescriptionLines;
        for (int i = 0; ; i++)
        {
            signatureLines = WordWrapping.WrapWords(selectedOverload.Signature, maxLength: availableWidth, maxLines: dedicatedLines[0]);
            summaryLines = WordWrapping.WrapWords(selectedOverload.Summary, maxLength: availableWidth, maxLines: dedicatedLines[1]);

            if (selectedOverload.Parameters.Count > 0)
            {
                var paramIndex = selectedArgumentIndex.Clamp(0, selectedOverload.Parameters.Count - 1);
                var param = selectedOverload.Parameters[paramIndex];
                var name = new FormattedString(param.Name, new ConsoleFormat(Bold: true));
                paramDescriptionLines = WordWrapping.WrapWords(spacesUnderCounter + name + ": " + param.Description, maxLength: availableWidth, maxLines: dedicatedLines[2]);
            }
            else
            {
                paramDescriptionLines = Array.Empty<FormattedString>();
            }

            if (GetHeight(signatureLines.Count, summaryLines.Count, paramDescriptionLines.Count) <= availableHeight)
            {
                break;
            }

            if (i == 0)
            {
                dedicatedLines[0] = signatureLines.Count;
                dedicatedLines[1] = summaryLines.Count;
                dedicatedLines[2] = paramDescriptionLines.Count;
            }

            dedicatedLines[dedicatedLines.ArgMax()]--;
        }

        var selectedItemLines = new List<FormattedString>();
        AddFinalLines(signatureLines);
        AddFinalLines(summaryLines);
        AddFinalLines(paramDescriptionLines);
        SelectedItem = selectedItemLines;

        Width =
            boxDrawingHorizontalBordersWidth +
            selectedItemLines.Max(l => l.GetUnicodeWidth());

        void AddFinalLines(IReadOnlyList<FormattedString> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                AddFinalLine(lines[i]);
            }
            void AddFinalLine(FormattedString text)
            {
                selectedItemLines.Add((selectedItemLines.Count == 0 ? counter : spacesUnderCounter) + text);
            }
        }
    }

    Task IKeyPressHandler.OnKeyDown(KeyPress key, CancellationToken cancellationToken)
    {
        if (!key.Handled && IsOpen)
        {
            switch (key.ObjectPattern)
            {
                case Escape:
                    Close();
                    break;
                case DownArrow:
                case UpArrow:
                    patternToProcessInKeyUp = key.ObjectPattern;
                    key.Handled = true;
                    break;
                default:
                    break;
            }
        }
        return Task.CompletedTask;
    }

    async Task IKeyPressHandler.OnKeyUp(KeyPress key, CancellationToken cancellationToken)
    {
        if (!IsOpen)
        {
            if (configuration.KeyBindings.TriggerOverloadList.Matches(key.ConsoleKeyInfo))
            {
                await TryOpen(preferedSelectedIndex: 0, cancellationToken).ConfigureAwait(false);
            }
        }

        if (IsOpen)
        {
            var oldSelectedIndex = SelectedItemIndex;
            Close();
            if (await TryOpen(oldSelectedIndex, cancellationToken).ConfigureAwait(false) &&
                patternToProcessInKeyUp != null)
            {
                switch (key.ObjectPattern)
                {
                    case DownArrow:
                        ++SelectedItemIndex;
                        key.Handled = true;
                        break;
                    case UpArrow:
                        --SelectedItemIndex;
                        key.Handled = true;
                        break;
                    default:
                        Debug.Fail("should not happen");
                        break;
                }
            }
        }

        patternToProcessInKeyUp = null;
    }
}