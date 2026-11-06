using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// <c>for: { variable: [values] }</c>, which runs its body once per value.
/// </summary>
internal sealed class ForNode : ScenarioNode
{
    public ForNode(SourceLocation location, string variable, IReadOnlyList<string> values, IReadOnlyList<ScenarioNode> steps)
        : base(location)
    {
        Variable = variable;
        Values = values;
        Steps = steps;
    }

    public string Variable { get; }

    public IReadOnlyList<string> Values { get; }

    public IReadOnlyList<ScenarioNode> Steps { get; }
}
