namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A C# test, an <see cref="InGameTestAttribute"/> method, as the runner and the CLI see it.
/// </summary>
public sealed class TestDescriptor
{
    /// <summary>
    /// The class's name without its namespace.
    /// </summary>
    public required string ClassName { get; init; }

    public required string MethodName { get; init; }

    /// <summary>
    /// The class's full name, to find it by.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// The name of the state it starts in, such as <c>editor</c>.
    /// </summary>
    public required string Start { get; init; }

    /// <summary>
    /// The level file it opens in the editor, relative to the <c>scenarios</c> folder, or null for a new level.
    /// </summary>
    public string Level { get; init; }

    /// <summary>
    /// The UID of the level it plays in free play, or null when it doesn't start there.
    /// </summary>
    public string FreePlayUid { get; init; }

    public string Documentation { get; init; }

    /// <summary>
    /// What the results call it, such as <c>WindowTests.MinimumHeight</c>.
    /// </summary>
    public string Name => $"{ClassName}.{MethodName}";

    /// <summary>
    /// Whether <paramref name="selector"/> names this test, or its class to run each test in it.
    /// </summary>
    public bool IsSelectedBy(string selector)
    {
        return selector == Name || selector == ClassName;
    }
}
