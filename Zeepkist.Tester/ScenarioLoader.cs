using System;
using System.Collections.Generic;
using System.IO;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Reads scenario files and the <c>run:</c> files they use, so a mistake in any of them fails before a run gets there.
/// </summary>
internal sealed class ScenarioLoader
{
    private readonly IReadOnlyList<StepDescriptor> _steps;
    private readonly Dictionary<string, Scenario> _loaded = [];

    public ScenarioLoader(IReadOnlyList<StepDescriptor> steps)
    {
        _steps = steps;
    }

    /// <exception cref="ScenarioException">The file or a file it runs can't be read as a scenario, or they run in a circle.</exception>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file can't be read.</exception>
    public Scenario Load(string file)
    {
        var scenario = Parse(file);
        RequireRuns(scenario.File, scenario.Steps, [file]);
        return scenario;
    }

    /// <summary>
    /// A file already loaded, for a <c>run:</c> of it.
    /// </summary>
    public Scenario Loaded(string file)
    {
        return _loaded[file];
    }

    private Scenario Parse(string file)
    {
        if (!_loaded.TryGetValue(file, out var scenario))
        {
            using var reader = new StreamReader(file);
            scenario = ScenarioParser.Parse(file, reader, _steps);
            _loaded.Add(file, scenario);
        }

        return scenario;
    }

    private void RequireRuns(string file, IReadOnlyList<ScenarioNode> nodes, List<string> running)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case ForNode loop:
                    RequireRuns(file, loop.Steps, running);
                    break;
                case RepeatNode repeat:
                    RequireRuns(file, repeat.Steps, running);
                    break;
                case RunNode run:
                    RequireRun(file, run, running);
                    break;
            }
        }
    }

    private void RequireRun(string file, RunNode run, List<string> running)
    {
        var target = ScenarioPaths.FromScenario(file, run.File);
        if (!File.Exists(target))
            throw new ScenarioException(run.Location, ScenarioMessages.RunFileMissing(run.File));

        // The same file written two ways is still the same file
        var fullPath = Path.GetFullPath(target);
        if (running.Exists(other => Path.GetFullPath(other) == fullPath))
            throw new ScenarioException(run.Location, ScenarioMessages.RunAlreadyRunning(run.File));

        var scenario = Parse(target);
        running.Add(target);
        RequireRuns(target, scenario.Steps, running);
        running.RemoveAt(running.Count - 1);
    }
}
