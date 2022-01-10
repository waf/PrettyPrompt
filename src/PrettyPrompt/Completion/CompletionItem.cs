#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Threading.Tasks;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt.Completion;

/// <summary>
/// A menu item in the Completion Menu Pane.
/// </summary>
public class CompletionItem
{
    /// <summary>
    /// The start index of the text that should be replaced
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// When the completion item is selected, this text will be inserted into the document at the specified start index.
    /// </summary>
    public string ReplacementText { get; }

    /// <summary>
    /// This text will be displayed in the completion menu. If not specified, the replacement text will be used.
    /// </summary>
    public FormattedString DisplayText { get; }

    /// <summary>
    /// This lazy task will be executed when the item is selected, to display the extended "tool tip" description to the right of the menu.
    /// </summary>
    public Lazy<Task<FormattedString>>? ExtendedDescription { get; }

    /// <param name="startIndex">The start index of the text that should be replaced</param>
    /// <param name="replacementText">When the completion item is selected, this text will be inserted into the document at the specified start index.</param>
    /// <param name="displayText">This text will be displayed in the completion menu. If not specified, the replacement text will be used.</param>
    /// <param name="extendedDescription">This lazy task will be executed when the item is selected, to display the extended "tool tip" description to the right of the menu.</param>
    public CompletionItem(int startIndex, string replacementText, FormattedString displayText, Lazy<Task<FormattedString>>? extendedDescription)
    {
        StartIndex = startIndex;
        ReplacementText = replacementText;
        DisplayText = displayText;
        ExtendedDescription = extendedDescription;
    }
}
