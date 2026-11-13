using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// What reading a scenario file found: the scenario when nothing is wrong with it, and otherwise each problem the
/// parser could go on after, in the order it found them.
/// </summary>
internal sealed class ScenarioReading
{
    public ScenarioReading(Scenario scenario, IReadOnlyList<ScenarioException> problems)
    {
        Scenario = scenario;
        Problems = problems;
    }

    /// <summary>
    /// Null when there are problems.
    /// </summary>
    public Scenario Scenario { get; }

    public IReadOnlyList<ScenarioException> Problems { get; }
}
