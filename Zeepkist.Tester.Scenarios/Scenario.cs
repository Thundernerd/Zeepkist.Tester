using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A scenario file, as it was written: nothing in it has been substituted or converted yet.
/// </summary>
internal sealed class Scenario
{
    public Scenario(string file, string description, IReadOnlyList<string> use, ScenarioStart start, InputRoute input, IReadOnlyList<ScenarioNode> steps)
    {
        File = file;
        Description = description;
        Use = use;
        Start = start;
        Input = input;
        Steps = steps;
    }

    public string File { get; }

    /// <summary>
    /// Null when the header leaves it out.
    /// </summary>
    public string Description { get; }

    public IReadOnlyList<string> Use { get; }

    /// <summary>
    /// <see cref="ScenarioStart.Default"/> when the header leaves it out.
    /// </summary>
    public ScenarioStart Start { get; }

    /// <summary>
    /// The route the scenario's input starts on, <see cref="InputRoute.InProcess"/> when the header leaves it out.
    /// </summary>
    public InputRoute Input { get; }

    public IReadOnlyList<ScenarioNode> Steps { get; }
}
