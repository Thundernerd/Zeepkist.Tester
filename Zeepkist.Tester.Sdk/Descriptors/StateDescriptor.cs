namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A state as the checks and the docs see it.
/// </summary>
public sealed class StateDescriptor
{
    public required string Name { get; init; }

    /// <summary>
    /// Empty for the tester's own states.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// The <see cref="FullName"/> of the state this one nests in, or null.
    /// </summary>
    public string Within { get; init; }

    public string Hint { get; init; }

    public string Documentation { get; init; }

    /// <summary>
    /// The name messages and step descriptors use, such as <c>editor</c> or <c>zls.window-open</c>.
    /// </summary>
    public string FullName => Namespace.Length == 0 ? Name : $"{Namespace}.{Name}";
}
