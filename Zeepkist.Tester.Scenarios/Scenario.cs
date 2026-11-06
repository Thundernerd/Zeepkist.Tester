using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A scenario file, as it was written: nothing in it has been substituted or converted yet.
/// </summary>
internal sealed class Scenario
{
    public Scenario(string file, string description, IReadOnlyList<string> use, ScenarioStart start, string input, IReadOnlyList<ScenarioNode> steps)
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
    /// Null when the header leaves it out, as it is for <see cref="Start"/> and <see cref="Input"/>.
    /// </summary>
    public string Description { get; }

    public IReadOnlyList<string> Use { get; }

    public ScenarioStart Start { get; }

    /// <summary>
    /// <c>in-process</c> or <c>os</c>.
    /// </summary>
    public string Input { get; }

    public IReadOnlyList<ScenarioNode> Steps { get; }
}
