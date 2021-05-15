using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrettyPrompt.Completion
{
    public delegate Task<IReadOnlyList<CompletionItem>> CompletionCallbackAsync(string text, int caret);

    /// <summary>
    /// A menu item in the Completion Menu Pane.
    /// </summary>
    public class CompletionItem
    {
        /// <summary>
        /// The start index of the text that should be replaced
        /// </summary>
        public int StartIndex { get; init; }

        /// <summary>
        /// When the completion item is selected, this text will be inserted into the document at the specified start index.
        /// </summary>
        public string ReplacementText { get; init; }

        /// <summary>
        /// This text will be displayed in the completion menu. If not specified, the replacement text will be used.
        /// </summary>
        public string DisplayText { get; init; }

        /// <summary>
        /// This lazy task will be executed when the item is selected, to display the extended "tool tip" description to the right of the menu.
        /// </summary>
        public Lazy<Task<string>> ExtendedDescription { get; init; }
    }
}
