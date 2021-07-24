using System;
using System.Collections.Generic;

namespace PrettyPrompt.Documents
{
    /// <summary>
    /// Stores undo/redo history for a single document.
    /// </summary>
    /// <remarks>
    /// Implementation is naive -- it just stores full snapshots in a linked list and undo/redo navigates
    /// through it. If tracking snapshots of the input ends up causing high memory, we can investigate more .
    /// </remarks>
    internal sealed class UndoRedoHistory
    {
        private readonly LinkedList<Document> history = new();
        private LinkedListNode<Document> currentUndoRedoEntry = null;

        internal void Track(Document document)
        {
            if (currentUndoRedoEntry is not null && currentUndoRedoEntry.Value.Equals(document)) return;
            history.AddLast(document.Clone());
            currentUndoRedoEntry = history.Last;
        }

        public Document Undo()
        {
            if(currentUndoRedoEntry?.Previous is not null)
            {
                currentUndoRedoEntry = currentUndoRedoEntry.Previous;
            }
            return currentUndoRedoEntry?.Value ?? new Document();
        }

        public Document Redo()
        {
            if(currentUndoRedoEntry?.Next is not null)
            {
                currentUndoRedoEntry = currentUndoRedoEntry.Next;
            }
            return currentUndoRedoEntry?.Value ?? new Document();
        }

        internal void Discard()
        {
            history.Clear();
            currentUndoRedoEntry = null;
        }
    }
}
