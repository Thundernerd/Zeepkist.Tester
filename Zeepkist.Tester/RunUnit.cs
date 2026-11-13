using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// One scenario or C# test in a run. Both run alike, with hooks around them, from where they start.
/// </summary>
internal sealed class RunUnit
{
    private readonly Func<RunUnit, IEnumerator> _body;

    /// <param name="context">What the unit keeps while it runs.</param>
    /// <param name="file">The scenario's file, or empty for a C# test.</param>
    /// <param name="start">Where it starts.</param>
    /// <param name="levelFolder">What a level file in <paramref name="start"/> is written relative to.</param>
    /// <param name="inputRoute">How its input reaches the game.</param>
    /// <param name="body">Runs its steps, once it's where it starts.</param>
    public RunUnit(ScenarioContext context, string file, ScenarioStart start, string levelFolder, InputRoute inputRoute, Func<RunUnit, IEnumerator> body)
    {
        Context = context;
        File = file;
        Start = start;
        LevelFolder = levelFolder;
        InputRoute = inputRoute;
        _body = body;
    }

    /// <summary>
    /// Empty for a C# test.
    /// </summary>
    public string File { get; }

    public ScenarioStart Start { get; }

    /// <summary>
    /// What a level file in <see cref="Start"/> is written relative to: the scenario's own folder, or the <c>scenarios</c>
    /// folder for a C# test.
    /// </summary>
    public string LevelFolder { get; }

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
