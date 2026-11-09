using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Every step and state that scenarios can run against, and the C# tests that run them.
/// </summary>
public sealed class StepRegistry
{
    public required IReadOnlyList<StepDescriptor> Steps { get; init; }

    public required IReadOnlyList<StateDescriptor> States { get; init; }

    /// <summary>
    /// The C# tests of the test plugins.
    /// </summary>
    public IReadOnlyList<TestDescriptor> Tests { get; init; } = [];
}
