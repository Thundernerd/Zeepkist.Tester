using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// <c>repeat: N</c>, which runs its body N times.
/// </summary>
internal sealed class RepeatNode : ScenarioNode
{
    public RepeatNode(SourceLocation location, int count, IReadOnlyList<ScenarioNode> steps)
        : base(location)
    {
        Count = count;
        Steps = steps;
    }

    public int Count { get; }

    public IReadOnlyList<ScenarioNode> Steps { get; }
}
