using System.Collections.Immutable;

namespace PrettyPrompt.Consoles;

//analogue of Microsoft.CodeAnalysis.Completion.CharacterSetModificationRule

/// <summary>
/// A rule that modifies a set of characters.
/// </summary>
public readonly struct CharacterSetModificationRule
{
    /// <summary>
    /// The kind of modification.
    /// </summary>
    public CharacterSetModificationKind Kind { get; }

    /// <summary>
    /// One or more characters.
    /// </summary>
    public ImmutableArray<char> Characters { get; }

    public CharacterSetModificationRule(CharacterSetModificationKind kind, ImmutableArray<char> characters)
    {
        Kind = kind;
        Characters = characters;
    }
}