#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using PrettyPrompt.Documents;

namespace PrettyPrompt.Completion;

/// <summary>
/// Describes the document change applied when a "complex" <see cref="CompletionItem"/> is committed.
/// </summary>
public readonly struct CompletionEdit
{
    /// <summary>The span of existing document text to replace with <see cref="NewText"/>.</summary>
    public TextSpan SpanToReplace { get; }

    /// <summary>The text inserted in place of <see cref="SpanToReplace"/>.</summary>
    public string NewText { get; }

    /// <summary>
    /// The absolute caret index (in the edited document) to move the caret to after applying the edit.
    /// When null, the caret is placed at the end of the inserted text.
    /// </summary>
    public int? NewCaret { get; }

    public CompletionEdit(TextSpan spanToReplace, string newText, int? newCaret = null)
    {
        SpanToReplace = spanToReplace;
        NewText = newText ?? string.Empty;
        NewCaret = newCaret;
    }
}
