using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What a run runs one at a time: a scenario or a C# test, which run alike: hooks around them, from where they start, and
/// their steps.
/// </summary>
internal sealed class RunUnit
{
    private readonly Func<RunUnit, IEnumerator> _body;

    /// <param name="context">What the unit keeps while it runs.</param>
    /// <param name="file">The scenario's file, or empty for a C# test.</param>
    /// <param name="start">Where it starts.</param>
    /// <param name="inputRoute">How its input reaches the game.</param>
    /// <param name="body">Runs its steps, once it's where it starts.</param>
    public RunUnit(ScenarioContext context, string file, ScenarioStart start, InputRoute inputRoute, Func<RunUnit, IEnumerator> body)
    {
        Context = context;
        File = file;
        Start = start;
        InputRoute = inputRoute;
        _body = body;
    }

    /// <summary>
    /// Empty for a C# test.
    /// </summary>
    public string File { get; }

    public ScenarioStart Start { get; }

    public InputRoute InputRoute { get; }

    public ScenarioContext Context { get; }

    public string Name => Context.ScenarioName;

    public bool Stopped { get; set; }

    public bool StartFailed { get; set; }

    public IEnumerator RunBody()
    {
        return _body(this);
    }
}
