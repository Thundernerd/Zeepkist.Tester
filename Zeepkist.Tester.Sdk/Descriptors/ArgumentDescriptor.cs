namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// One argument of a step or of a nested argument class.
/// </summary>
public sealed class ArgumentDescriptor
{
    public required string Name { get; init; }

    public required ArgumentType Type { get; init; }

    public string Documentation { get; init; }

    public bool IsPrimary { get; init; }

    public bool IsRequired { get; init; }

    /// <summary>
    /// Null when the argument has none. An enum's default is the value as a scenario writes it.
    /// </summary>
    public object Default { get; init; }

    /// <summary>
    /// From <see cref="PatternAttribute"/>, or else <see cref="ArgumentType.Pattern"/>.
    /// </summary>
    public string Pattern { get; init; }

    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public int? MinItems { get; init; }
}
