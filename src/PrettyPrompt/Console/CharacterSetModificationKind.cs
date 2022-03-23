namespace PrettyPrompt.Consoles;

//analogue of Microsoft.CodeAnalysis.Completion.CharacterSetModificationKind

/// <summary>
/// The kind of character set modification.
/// </summary>
public enum CharacterSetModificationKind
{
    /// <summary>
    /// The rule adds new characters onto the existing set of characters.
    /// </summary>
    Add,

    /// <summary>
    /// The rule removes characters from the existing set of characters.
    /// </summary>
    Remove,

    /// <summary>
    /// The rule replaces the existing set of characters.
    /// </summary>
    Replace
}