using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The type of an argument, or of a list's items.
/// </summary>
public sealed class ArgumentType
{
    public required ArgumentKind Kind { get; init; }

    /// <summary>
    /// What a value of this type has to match, for the types that have such a form, as <see cref="FileName"/> does.
    /// </summary>
    public string Pattern { get; init; }

    /// <summary>
    /// For an <see cref="ArgumentKind.Enum"/>: its values as a scenario writes them.
    /// </summary>
    public IReadOnlyList<string> EnumValues { get; init; } = [];

    /// <summary>
    /// For a <see cref="ArgumentKind.List"/>: the type of its items.
    /// </summary>
    public ArgumentType ItemType { get; init; }

    /// <summary>
    /// For an <see cref="ArgumentKind.Object"/>: the nested class's documentation.
    /// </summary>
    public string Documentation { get; init; }

    /// <summary>
    /// For an <see cref="ArgumentKind.Object"/>: the nested class's arguments.
    /// </summary>
    public IReadOnlyList<ArgumentDescriptor> Arguments { get; init; } = [];
}
