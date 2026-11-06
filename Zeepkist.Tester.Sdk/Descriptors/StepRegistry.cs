using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Every step and state that scenarios can run against.
/// </summary>
public sealed class StepRegistry
{
    public required IReadOnlyList<StepDescriptor> Steps { get; init; }

    public required IReadOnlyList<StateDescriptor> States { get; init; }
}
