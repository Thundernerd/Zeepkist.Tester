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
    /// The kind of a <c>for</c> variable and a <c>with:</c> value.
    /// </summary>
    private const string TextKind = "text";

    private readonly IReadOnlyList<StepDescriptor> _steps;
    private readonly Dictionary<string, StateDescriptor> _states;
    private readonly Dictionary<string, Scenario> _scenarios = [];
    private readonly HashSet<string> _followed = [];
    private readonly List<string> _running = [];
    private readonly List<CheckProblem> _problems = [];
    private readonly HashSet<string> _reported = [];
    private SourceLocation _ranFrom;

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

        // A file that can't be read may be what runs the others, and what they'd be given can't be known
        var mayHaveAHiddenCaller = _scenarios.Values.Contains(null);
        foreach (var scenario in scenarios.Where(scenario => !runByAFile.Contains(Key(scenario.File))))
            FollowFromItsStart(scenario, mayHaveAHiddenCaller);

        // What's left only runs in a cycle, which following any of it reports, so its caller is in there somewhere too
        foreach (var scenario in scenarios.Where(scenario => !_followed.Contains(Key(scenario.File))))
            FollowFromItsStart(scenario, true);

        return _problems
            .OrderBy(problem => problem.Location.File, StringComparer.Ordinal)
            .ThenBy(problem => problem.Location.Line)
            .ThenBy(problem => problem.Location.Column)
            .ToList();
    }

    private Scenario Load(string file)
    {
        var key = Key(file);
        if (_scenarios.TryGetValue(key, out var loaded))
            return loaded;

        Scenario scenario = null;
        try
        {
            using var reader = File.OpenText(file);
            scenario = ScenarioParser.Parse(file, reader, _steps);
        }
        catch (ScenarioException exception)
        {
            Add(new CheckProblem(exception.Location, true, exception.Problem));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Add(new CheckProblem(new SourceLocation(file, 1, 1), true, $"Can't read the file: {exception.Message}"));
        }

        _scenarios.Add(key, scenario);
        return scenario;
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
                    if (files.Add(Key(target)) && File.Exists(target) && Load(target) is { } scenario)
                        CollectRuns(scenario, files);

                    break;
            }
        }
    }

    private void FollowFromItsStart(Scenario scenario, bool mayBeGivenNames)
    {
        var start = scenario.Start?.Name ?? ScenarioStart.DefaultName;
        Follow(scenario, new Scope(null) { DefinesUnknownNames = mayBeGivenNames }, [start]);
    }

    private void Follow(Scenario scenario, Scope scope, HashSet<string> held)
    {
        var key = Key(scenario.File);
        _followed.Add(key);
        _running.Add(key);
        Follow(scenario.File, scenario.Steps, scope, held);
        _running.RemoveAt(_running.Count - 1);
    }

    private void Follow(string file, IReadOnlyList<ScenarioNode> nodes, Scope scope, HashSet<string> held)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case StepNode step:
                    FollowStep(step, scope, held);
                    break;
                case ForNode loop:
                    FollowFor(file, loop, scope, held);
                    break;
                case RepeatNode repeat:
                    for (var pass = 0; pass < Math.Min(repeat.Count, 2); pass++)
                        Follow(file, repeat.Steps, new Scope(scope), held);

                    break;
                case RunNode run:
                    FollowRun(file, run, scope, held);
                    break;
            }
        }
    }

    private void FollowFor(string file, ForNode loop, Scope scope, HashSet<string> held)
    {
        var outer = scope.Find(loop.Variable);
        if (outer != null)
            Report(loop.Location, true, AlreadyDefined(loop.Variable, outer));

        foreach (var value in loop.Values)
        {
            var body = new Scope(scope);
            body.Define(loop.Variable, new NamedValue([TextKind], true, value));
            Follow(file, loop.Steps, body, held);
        }
    }

    private void FollowRun(string file, RunNode run, Scope scope, HashSet<string> held)
    {
        // A run: file sees only what its with: passes in. Its text is known unless it uses a value only a run can know
        var given = new Scope(null);
        foreach (var entry in run.With)
        {
            var text = CheckNames(entry.Value, scope) ? Substitution.Replace(entry.Value.Text, written => scope.Find(written).Value) : null;
            given.Define(entry.Key, new NamedValue([TextKind], true, text));
        }

        var target = RunTarget(file, run);
        if (!File.Exists(target))
        {
            Report(run.Location, true, $"run: can't find '{run.File}' from this file.");
            return;
        }

        if (_running.Contains(Key(target)))
        {
            Report(run.Location, true, $"run: '{run.File}' is already running here, so it would never end.");
            return;
        }

        var scenario = Load(target);
        if (scenario == null)
            return;

        var ranFrom = _ranFrom;
        _ranFrom = run.Location;
        Follow(scenario, given, held);
        _ranFrom = ranFrom;
    }

    private void FollowStep(StepNode node, Scope scope, HashSet<string> held)
    {
        // A step's values are all read before it runs, so what it defines is only there for the next step
        var defines = new List<(string Name, ArgumentType Type, SourceLocation Location)>();
        FollowArguments(node.Step.Arguments, node.Arguments, scope, defines);
        foreach (var (name, type, location) in defines)
        {
            if (name == null)
            {
                // It can define any name, so what's used after it can't be called undefined
                scope.DefinesUnknownNames = true;
                continue;
            }

            var defined = scope.Find(name);
            if (defined != null)
                Report(location, true, AlreadyDefined(name, defined));
            else
                scope.Define(name, new NamedValue([type.ValueKind, .. type.BaseValueKinds], type.Substitute != null, null));
        }

        FollowStates(node, held);
    }

    private void FollowArguments(IReadOnlyList<ArgumentDescriptor> arguments, ObjectArgument written, Scope scope, List<(string, ArgumentType, SourceLocation)> defines)
    {
        foreach (var argument in arguments)
        {
            if (written.Values.TryGetValue(argument.Name, out var value))
                FollowValue(argument.Type, value, scope, defines);
            else if (argument.Default != null && argument.Type.Kind is ArgumentKind.Name or ArgumentKind.Ref)
                FollowName(argument.Type, WrittenValue.DefaultText(argument.Default), written.Location, scope, defines);
        }
    }

    private void FollowValue(ArgumentType type, ArgumentValue value, Scope scope, List<(string, ArgumentType, SourceLocation)> defines)
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

    private void FollowScalar(ArgumentType type, ScalarArgument scalar, Scope scope, List<(string, ArgumentType, SourceLocation)> defines)
    {
        var isKnown = CheckNames(scalar, scope);
        if (type.Kind != ArgumentKind.Name && type.Kind != ArgumentKind.Ref)
            return;

        if (!isKnown)
        {
            if (type.Kind == ArgumentKind.Name)
                defines.Add((null, type, scalar.Location));

            return;
        }

        FollowName(type, Substitution.Replace(scalar.Text, written => scope.Find(written).Value), scalar.Location, scope, defines);
    }

    private void FollowName(ArgumentType type, string name, SourceLocation location, Scope scope, List<(string, ArgumentType, SourceLocation)> defines)
    {
        if (type.Kind == ArgumentKind.Name)
        {
            defines.Add((name, type, location));
            return;
        }

        var value = scope.Find(name);
        if (value == null && !scope.MayDefineUnknownNames)
            Report(location, true, NotDefined(name));
        else if (value != null && !value.Kinds.Contains(type.ValueKind))
            Report(location, true, $"The name '{name}' is a {value.Kind}, not a {type.ValueKind}.");
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
                    Report(scalar.Location, true, NotDefined(name));
            }
            else if (!value.CanBeWritten)
            {
                Report(scalar.Location, true, $"${{{name}}} can't be written: a {value.Kind} has no Substitute member.");
            }

            isKnown &= value?.CanBeWritten == true && value.Value != null;
        }

        return isKnown;
    }

    private void FollowStates(StepNode node, HashSet<string> held)
    {
        foreach (var alternatives in node.Step.Requires)
        {
            var unmet = alternatives.Select(state => OutermostUnmet(state, held)).TakeWhile(state => state != null).ToList();
            if (unmet.Count == alternatives.Count)
                Report(node.Location, false, node.Step.NeedsMessage(unmet.Distinct()));
        }

        foreach (var state in node.Step.Leaves)
            Leave(state, held);

        foreach (var state in node.Step.Enters)
            held.Add(state);
    }

    /// <summary>
    /// As the harness checks it: a state only holds when the ones around it do. Null when it holds, and otherwise the state to name, because that's the one to get into first.
    /// </summary>
    private StateDescriptor OutermostUnmet(string name, HashSet<string> held)
    {
        var state = _states[name];
        var outer = state.Within == null ? null : OutermostUnmet(state.Within, held);
        return outer ?? (held.Contains(name) ? null : state);
    }

    private void Leave(string name, HashSet<string> held)
    {
        held.Remove(name);
        foreach (var inner in _states.Values.Where(state => state.Within == name))
            Leave(inner.FullName, held);
    }

    private static string RunTarget(string file, RunNode run)
    {
        return Path.Combine(Path.GetDirectoryName(file), run.File.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string Key(string file)
    {
        return Path.GetFullPath(file);
    }

    private static string NotDefined(string name)
    {
        return $"The name '{name}' isn't defined by an earlier step.";
    }

    private static string AlreadyDefined(string name, NamedValue defined)
    {
        return $"The name '{name}' is already defined, as a {defined.Kind}.";
    }

    private void Report(SourceLocation location, bool isError, string message)
    {
        Add(new CheckProblem(location, isError, _ranFrom == null ? message : $"{message} (run from {_ranFrom})"));
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
