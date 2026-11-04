using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A step as the parser, the schema and the docs see it.
/// </summary>
public sealed class StepDescriptor
{
    public required string Name { get; init; }

    /// <summary>
    /// Empty for the tester's own steps.
    /// </summary>
    public required string Namespace { get; init; }

    public required IReadOnlyList<ArgumentDescriptor> Arguments { get; init; }

    public string Documentation { get; init; }

    public IReadOnlyList<string> Examples { get; init; } = [];

    /// <summary>
    /// The name a scenario writes without <c>use:</c>, such as <c>wait</c> or <c>zls.compile</c>.
    /// </summary>
    public string FullName => Namespace.Length == 0 ? Name : $"{Namespace}.{Name}";
}
