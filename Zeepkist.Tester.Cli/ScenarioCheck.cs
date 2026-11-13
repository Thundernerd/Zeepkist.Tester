using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// Follows scenarios the way a run would, without the game: which names exist at each step are errors, because they can
/// only go one way, and which states hold are warnings, because the model is only as good as what steps declare.
/// </summary>
/// <remarks>
/// A <c>for</c> is followed once per value and a <c>repeat</c> at most twice, each pass from the state the last one ended
/// in, so a body that closes the window it needs is caught, and one that only runs once isn't blamed for it.
/// </remarks>
internal sealed class ScenarioCheck
{
    /// <summary>
    /// Nested bodies and <c>run:</c> files multiply, and following them all could take longer than a run would.
    /// </summary>
    private const int MostNodesFollowed = 1_000_000;

    private readonly IReadOnlyList<StepDescriptor> _steps;
    private readonly Dictionary<string, StateDescriptor> _states;
    private readonly Dictionary<string, Scenario> _scenarios = [];
    private readonly List<string> _brokenFiles = [];
    private readonly HashSet<string> _followed = [];
    private readonly List<string> _running = [];
    private readonly List<CheckProblem> _problems = [];
    private readonly HashSet<string> _reported = [];
    private HeldStates _held;
    private SourceLocation _ranFrom;
    private int _nodesFollowed;

    private ScenarioCheck(StepRegistry registry)
    {
        _steps = registry.Steps;
        _states = registry.States.ToDictionary(state => state.FullName);
    }

    /// <summary>
    /// Checks each file, in order of file, line and column. A file that another of the files runs is only followed from
    /// there, because it may need what that passes in through <c>with:</c>.
    /// </summary>
    public static IReadOnlyList<CheckProblem> Run(IEnumerable<string> files, StepRegistry registry)
    {
        return new ScenarioCheck(registry).CheckFiles(files.ToList());
    }

    private List<CheckProblem> CheckFiles(List<string> files)
    {
        var scenarios = files.Select(Load).Where(scenario => scenario != null).Distinct().ToList();

        var runByAFile = new HashSet<string>();
        foreach (var scenario in scenarios)
            CollectRuns(scenario, runByAFile);

        foreach (var scenario in scenarios.Where(scenario => !runByAFile.Contains(FullPathOf(scenario.File))))
            FollowFromItsStart(scenario, MayBeRunByABrokenFile(scenario));

        // What's left only runs in a cycle, which following any of it reports, so its caller is in there somewhere too
        foreach (var scenario in scenarios.Where(scenario => !_followed.Contains(FullPathOf(scenario.File))))
            FollowFromItsStart(scenario, true);

        return _problems
            .OrderBy(problem => problem.Location.File, StringComparer.Ordinal)
            .ThenBy(problem => problem.Location.Line)
            .ThenBy(problem => problem.Location.Column)
            .ToList();
    }

    private Scenario Load(string file)
    {
        var key = FullPathOf(file);
        if (_scenarios.TryGetValue(key, out var loaded))
            return loaded;

        Scenario scenario = null;
        string text = null;
        try
        {
            text = File.ReadAllText(file);
            scenario = ScenarioParser.Parse(file, new StringReader(text), _steps);
        }
        catch (ScenarioException exception)
        {
            _brokenFiles.Add(text);
            Add(new CheckProblem(exception.Location, CheckSeverity.Error, exception.Problem));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Add(new CheckProblem(new SourceLocation(file, 1, 1), CheckSeverity.Error, $"Can't read the file: {exception.Message}"));
        }

        _scenarios.Add(key, scenario);
        return scenario;
    }

    /// <summary>
    /// A file that doesn't parse has no <c>run:</c> nodes to follow, so all that shows it may give this one names is that it mentions it.
    /// </summary>
    private bool MayBeRunByABrokenFile(Scenario scenario)
    {
        var name = Path.GetFileName(scenario.File);
        return _brokenFiles.Any(text => text.Contains(name, StringComparison.Ordinal));
    }

    private void CollectRuns(Scenario scenario, HashSet<string> files)
    {
        CollectRuns(scenario.File, scenario.Steps, files);
    }

    private void CollectRuns(string file, IReadOnlyList<ScenarioNode> nodes, HashSet<string> files)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case ForNode loop:
                    CollectRuns(file, loop.Steps, files);
                    break;
                case RepeatNode repeat:
                    CollectRuns(file, repeat.Steps, files);
                    break;
                case RunNode run:
                    var target = RunTarget(file, run);
                    if (files.Add(FullPathOf(target)) && File.Exists(target) && Load(target) is { } scenario)
                        CollectRuns(scenario, files);

                    break;
            }
        }
    }

    private void FollowFromItsStart(Scenario scenario, bool mayBeGivenNames)
    {
        _nodesFollowed = 0;
        var level = scenario.Start.LevelPath(scenario.File);
        if (level != null && !File.Exists(level))
            Report(scenario.Start.Location, CheckSeverity.Error, $"start: {scenario.Start.State.Name} can't find '{scenario.Start.Value}' from this file.");

        _held = new HeldStates(_states, scenario.Start.State.Name);
        Follow(scenario, new Scope(null) { DefinesUnknownNames = mayBeGivenNames });
    }

    private void Follow(Scenario scenario, Scope scope)
    {
        var key = FullPathOf(scenario.File);
        _followed.Add(key);
        _running.Add(key);
        Follow(scenario.File, scenario.Steps, scope);
        _running.RemoveAt(_running.Count - 1);
    }

    private void Follow(string file, IReadOnlyList<ScenarioNode> nodes, Scope scope)
    {
        foreach (var node in nodes)
        {
            if (++_nodesFollowed > MostNodesFollowed)
            {
                // Each body around this one still has its next pass to get through
                if (_nodesFollowed == MostNodesFollowed + 1)
                    Report(node.Location, CheckSeverity.Warning, $"The loops and run: files around this come to over {MostNodesFollowed} steps, so the check stops following it here.");

                return;
            }

            switch (node)
            {
                case StepNode step:
                    FollowStep(step, scope);
                    break;
                case ForNode loop:
                    FollowFor(file, loop, scope);
                    break;
                case RepeatNode repeat:
                    for (var pass = 0; pass < Math.Min(repeat.Count, 2); pass++)
                        Follow(file, repeat.Steps, new Scope(scope));

                    break;
                case RunNode run:
                    FollowRun(file, run, scope);
                    break;
            }
        }
    }

    private void FollowFor(string file, ForNode loop, Scope scope)
    {
        var outer = scope.Find(loop.Variable);
        if (outer != null)
            Report(loop.Location, CheckSeverity.Error, ScenarioMessages.AlreadyDefined(loop.Variable, outer.Kind));

        foreach (var value in loop.Values)
        {
            var body = new Scope(scope);
            body.Define(loop.Variable, new NamedValue([TextValue.Kind], true, value));
            Follow(file, loop.Steps, body);
        }
    }

    private void FollowRun(string file, RunNode run, Scope scope)
    {
        // A run: file sees only what its with: passes in. Its text is known unless it uses a value only a run can know
        var given = new Scope(null);
        foreach (var entry in run.With)
        {
            var text = CheckNames(entry.Value, scope) ? Substitution.Replace(entry.Value.Text, written => scope.Find(written).Value) : null;
            given.Define(entry.Key, new NamedValue([TextValue.Kind], true, text));
        }

        var target = RunTarget(file, run);
        if (!File.Exists(target))
        {
            Report(run.Location, CheckSeverity.Error, ScenarioMessages.RunFileMissing(run.File));
            return;
        }

        if (_running.Contains(FullPathOf(target)))
        {
            Report(run.Location, CheckSeverity.Error, ScenarioMessages.RunAlreadyRunning(run.File));
            return;
        }

        var scenario = Load(target);
        if (scenario == null)
            return;

        var ranFrom = _ranFrom;
        _ranFrom = run.Location;
        Follow(scenario, given);
        _ranFrom = ranFrom;
    }

    private void FollowStep(StepNode node, Scope scope)
    {
        // A step's values are all read before it runs, so what it defines is only there for the next step
        var defines = new List<Definition>();
        FollowArguments(node.Step.Arguments, node.Arguments, scope, defines);
        foreach (var definition in defines)
        {
            if (definition.Name == null)
            {
                // It can define any name, so what's used after it can't be called undefined
                scope.DefinesUnknownNames = true;
                continue;
            }

            var type = definition.Type;
            var defined = scope.Find(definition.Name);
            if (defined != null)
                Report(definition.Location, CheckSeverity.Error, ScenarioMessages.AlreadyDefined(definition.Name, defined.Kind));
            else
                scope.Define(definition.Name, new NamedValue([type.ValueKind, .. type.BaseValueKinds], type.Substitute != null, null));
        }

        FollowStates(node);
    }

    private void FollowArguments(IReadOnlyList<ArgumentDescriptor> arguments, ObjectArgument written, Scope scope, List<Definition> defines)
    {
        foreach (var argument in arguments)
        {
            if (written.Values.TryGetValue(argument.Name, out var value))
                FollowValue(argument.Type, value, scope, defines);
            else if (argument.Default != null && argument.Type.Kind is ArgumentKind.Name or ArgumentKind.Ref)
                FollowName(argument.Type, WrittenValue.DefaultText(argument.Default), written.Location, scope, defines);
        }
    }

    private void FollowValue(ArgumentType type, ArgumentValue value, Scope scope, List<Definition> defines)
    {
        switch (value)
        {
            case ListArgument list:
                foreach (var item in list.Items)
                    FollowValue(type.ItemType, item, scope, defines);

                break;
            case ObjectArgument nested:
                FollowArguments(type.Arguments, nested, scope, defines);
                break;
            default:
                FollowScalar(type, (ScalarArgument)value, scope, defines);
                break;
        }
    }

    private void FollowScalar(ArgumentType type, ScalarArgument scalar, Scope scope, List<Definition> defines)
    {
        var isKnown = CheckNames(scalar, scope);
        if (type.Kind != ArgumentKind.Name && type.Kind != ArgumentKind.Ref)
            return;

        if (!isKnown)
        {
            if (type.Kind == ArgumentKind.Name)
                defines.Add(new Definition(null, type, scalar.Location));

            return;
        }

        FollowName(type, Substitution.Replace(scalar.Text, written => scope.Find(written).Value), scalar.Location, scope, defines);
    }

    private void FollowName(ArgumentType type, string name, SourceLocation location, Scope scope, List<Definition> defines)
    {
        if (type.Kind == ArgumentKind.Name)
        {
            defines.Add(new Definition(name, type, location));
            return;
        }

        var value = scope.Find(name);
        if (value == null && !scope.MayDefineUnknownNames)
            Report(location, CheckSeverity.Error, ScenarioMessages.NotDefined(name));
        else if (value != null && !value.Kinds.Contains(type.ValueKind))
            Report(location, CheckSeverity.Error, ScenarioMessages.WrongKind(name, value.Kind, type.ValueKind));
    }

    /// <summary>
    /// Reports each <c>${name}</c> that can't be written here, and says whether each one's value is known.
    /// </summary>
    private bool CheckNames(ScalarArgument scalar, Scope scope)
    {
        var isKnown = true;
        foreach (var name in Substitution.NamesIn(scalar.Text))
        {
            var value = scope.Find(name);
            if (value == null)
            {
                if (!scope.MayDefineUnknownNames)
                    Report(scalar.Location, CheckSeverity.Error, ScenarioMessages.NotDefined(name));
            }
            else if (!value.CanBeWritten)
            {
                Report(scalar.Location, CheckSeverity.Error, ScenarioMessages.NoSubstituteMember(name, value.Kind));
            }

            isKnown &= value?.CanBeWritten == true && value.Value != null;
        }

        return isKnown;
    }

    private void FollowStates(StepNode node)
    {
        foreach (var unmet in _held.Unmet(node.Step))
            Report(node.Location, CheckSeverity.Warning, ScenarioMessages.Needs(node.Step, unmet));

        _held.Apply(node.Step);
    }

    private static string RunTarget(string file, RunNode run)
    {
        return ScenarioPaths.FromScenario(file, run.File);
    }

    /// <summary>
    /// A path written in a file can be one no file can have, which <see cref="Path.GetFullPath(string)"/> throws for.
    /// </summary>
    private static string FullPathOf(string file)
    {
        return File.Exists(file) ? Path.GetFullPath(file) : file;
    }

    private void Report(SourceLocation location, CheckSeverity severity, string message)
    {
        Add(new CheckProblem(location, severity, _ranFrom == null ? message : $"{message} (run from {_ranFrom})"));
    }

    /// <summary>
    /// Passes of a body and files run twice find the same problem again.
    /// </summary>
    private void Add(CheckProblem problem)
    {
        if (_reported.Add(problem.ToString()))
            _problems.Add(problem);
    }

    /// <summary>
    /// A name a step defines. Its name is null when only a run can know it.
    /// </summary>
    private sealed class Definition
    {
        public Definition(string name, ArgumentType type, SourceLocation location)
        {
            Name = name;
            Type = type;
            Location = location;
        }

        public string Name { get; }

        public ArgumentType Type { get; }

        public SourceLocation Location { get; }
    }

    /// <summary>
    /// A named value as far as it's known offline. Its value is the text of a <c>for</c> variable in this pass or of a literal in <c>with:</c>, and otherwise null.
    /// </summary>
    private sealed class NamedValue
    {
        /// <param name="kinds">Its own kind, then those it also counts as.</param>
        /// <param name="canBeWritten">Whether <c>${name}</c> can write it.</param>
        /// <param name="value">The text, if it's known here.</param>
        public NamedValue(IReadOnlyList<string> kinds, bool canBeWritten, string value)
        {
            Kinds = kinds;
            CanBeWritten = canBeWritten;
            Value = value;
        }

        public IReadOnlyList<string> Kinds { get; }

        public string Kind => Kinds[0];

        public bool CanBeWritten { get; }

        public string Value { get; }
    }

    /// <summary>
    /// The names of one pass of a body or one <c>run:</c> file, which can read those of the scope around it.
    /// </summary>
    private sealed class Scope
    {
        private readonly Scope _outer;
        private readonly Dictionary<string, NamedValue> _values = [];

        public Scope(Scope outer)
        {
            _outer = outer;
        }

        public bool DefinesUnknownNames { get; set; }

        public bool MayDefineUnknownNames => DefinesUnknownNames || _outer?.MayDefineUnknownNames == true;

        public NamedValue Find(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : _outer?.Find(name);
        }

        public void Define(string name, NamedValue value)
        {
            _values[name] = value;
        }
    }
}
