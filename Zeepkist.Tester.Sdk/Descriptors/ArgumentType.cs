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
    /// For a <see cref="ArgumentKind.Name"/> or <see cref="ArgumentKind.Ref"/>: the kind of value, from <see cref="ValueKindAttribute.Name"/>.
    /// </summary>
    public string ValueKind { get; init; }

    /// <summary>
    /// For a <see cref="ArgumentKind.Name"/> or <see cref="ArgumentKind.Ref"/>: the kinds a value of this kind also counts as, because its class derives from theirs.
    /// </summary>
    public IReadOnlyList<string> BaseValueKinds { get; init; } = [];

    /// <summary>
    /// For a <see cref="ArgumentKind.Name"/> or <see cref="ArgumentKind.Ref"/>: the member <c>${name}</c> writes, or null when this kind can't be written that way.
    /// </summary>
    public string Substitute { get; init; }

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
