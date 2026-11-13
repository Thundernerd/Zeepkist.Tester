using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Runs scenarios in order, then C# tests. A failed step stops its scenario or test with a screenshot, and the run goes on
/// with the next. A start that fails ends the run, because that leaves the game somewhere unknown, and what follows there
/// would say nothing.
/// </summary>
internal sealed class ScenarioRunner
{
    private readonly IRunHost _host;
    private readonly ExceptionLog _exceptions;
    private readonly StepTypes _steps;
    private readonly IReadOnlyList<TestPluginRegistration> _plugins;
    private readonly ScenarioLoader _loader;
    private readonly string _folder;
    private readonly RunRecorder _recorder;
    private readonly InGameTestRunner _tests;

    /// <param name="host">The game.</param>
    /// <param name="exceptions">Where the game's exceptions are listed under each scenario.</param>
    /// <param name="catalog">The steps scenarios can use, and the C# tests a run can choose from.</param>
    /// <param name="plugins">What the test plugins gave.</param>
    /// <param name="report">Takes each step's result.</param>
    /// <param name="folder">What a scenario's name is relative to, so two files of one name in two folders stay apart, and a C# test's level file.</param>
    public ScenarioRunner(IRunHost host, ExceptionLog exceptions, RunCatalog catalog, IReadOnlyList<TestPluginRegistration> plugins, Action<RunStep> report, string folder)
    {
        _folder = folder;
        _host = host;
        _exceptions = exceptions;
        _steps = catalog.Steps;
        _plugins = plugins;
        _recorder = new RunRecorder(host, report);
        _tests = new InGameTestRunner(host, exceptions, catalog, _recorder, folder);
        _loader = new ScenarioLoader(catalog.Steps.Descriptors);
    }

    private IEnumerable<IScenarioHooks> Hooks => _plugins.SelectMany(plugin => plugin.ScenarioHooks);

    /// <param name="files">The scenario files to run, in order.</param>
    /// <param name="tests">The C# tests to run after them: each a test's name, or its class's for each test in it.</param>
    public IEnumerator Run(IReadOnlyList<string> files, IReadOnlyList<string> tests)
    {
        if (files.Count == 0 && tests.Count == 0)
        {
            _recorder.Report(RunStep.Fail("scenarios", "run.json names no scenarios or tests, so this run tested nothing."));
            yield break;
        }

        var run = new RunContext(_host.Logger, Services());
        var units = LoadAll(files).Select(scenario => UnitOf(scenario, run))
            .Concat(_tests.Select(tests, run))
            .ToList();
        if (units.Count == 0)
            yield break;

        if (!RunStartedHooks(run, out var problem))
        {
            ReportNotRun(units, problem);
            yield break;
        }

        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            yield return RunOne(unit, isFirst: i == 0);
            if (unit.StartFailed)
            {
                ReportNotRun(units.Skip(i + 1), $"{unit.Name} couldn't get to where it starts, so the game is somewhere unknown.");
                yield break;
            }
        }
    }

    private RunUnit UnitOf(Scenario scenario, RunContext run)
    {
        return new RunUnit(new ScenarioContext(run, ScenarioName(scenario.File)), scenario.File, scenario.Start, Path.GetDirectoryName(scenario.File), scenario.Input, unit => RunNodes(unit, scenario.Steps, Frame.Top(scenario.File)));
    }

    private string ScenarioName(string file)
    {
        var name = (file.StartsWith(_folder, StringComparison.Ordinal) ? file.Substring(_folder.Length) : Path.GetFileName(file))
            .Replace('\\', '/')
            .TrimStart('/');
        return ScenarioFiles.WithoutExtension(name);
    }

    /// <exception cref="StepDefinitionException">Two test plugins gave a service of one type.</exception>
    private Dictionary<Type, object> Services()
    {
        var services = new Dictionary<Type, object>();
        var givenBy = new Dictionary<Type, string>();
        foreach (var plugin in _plugins)
        {
            foreach (var service in plugin.Services)
            {
                if (givenBy.TryGetValue(service.Key, out var other))
                    throw new StepDefinitionException($"{other} and {plugin.PluginName} both gave a service of type {service.Key.FullName}.");

                givenBy.Add(service.Key, plugin.PluginName);
                services.Add(service.Key, service.Value);
            }
        }

        return services;
    }

    private List<Scenario> LoadAll(IReadOnlyList<string> files)
    {
        var scenarios = new List<Scenario>();
        foreach (var file in files)
        {
            try
            {
                scenarios.Add(_loader.Load(file));
            }
            catch (ScenarioException exception)
            {
                _recorder.Report(new RunStep { Name = "load", Scenario = ScenarioName(file), File = exception.Location.File, Line = (int)exception.Location.Line, Detail = exception.Message });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _recorder.Report(new RunStep { Name = "load", Scenario = ScenarioName(file), File = file, Detail = exception.Message });
            }
        }

        return scenarios;
    }

    private bool RunStartedHooks(RunContext run, out string problem)
    {
        foreach (var hooks in Hooks)
        {
            try
            {
                hooks.RunStarted(run);
            }
            catch (Exception exception)
            {
                var name = $"{hooks.GetType().Name}.{nameof(IScenarioHooks.RunStarted)}";
                _recorder.LogError(exception);
                _recorder.Report(RunStep.Fail(name, RunRecorder.Describe(exception)));
                problem = $"{name} failed.";
                return false;
            }
        }

        problem = null;
        return true;
    }

    private void ReportNotRun(IEnumerable<RunUnit> units, string why)
    {
        foreach (var unit in units)
            _recorder.Report(new RunStep { Name = "not run", Scenario = unit.Name, File = unit.File, Detail = why });
    }

    private IEnumerator RunOne(RunUnit unit, bool isFirst)
    {
        _host.Logger.LogInfo($"{TestRun.Marker} scenario {unit.Name}");
        _exceptions.StartScenario(unit.Name);
        _host.Input.Route = unit.InputRoute;

        var ready = true;
        if (!isFirst)
        {
            foreach (var hooks in Hooks)
                yield return RunHook(unit, hooks, nameof(IScenarioHooks.BeforeScenario), () => hooks.BeforeScenario(unit.Context), () => ready = false);
        }

        if (ready)
        {
            yield return EnterStart(unit);
            if (!unit.StartFailed)
                yield return unit.RunBody();
        }

        foreach (var hooks in Hooks)
            yield return RunHook(unit, hooks, nameof(IScenarioHooks.AfterScenario), () => hooks.AfterScenario(unit.Context), null);

        _host.Input.GiveBack();
    }

    private IEnumerator RunHook(RunUnit unit, IScenarioHooks hooks, string hook, Func<IEnumerator> routine, Action failed)
    {
        Exception error = null;
        yield return GuardedRoutine.Run(RunRecorder.Called(routine), exception => error = exception);
        if (error == null)
            yield break;

        _recorder.LogError(error);
        _recorder.Report(new RunStep { Name = $"{hooks.GetType().Name}.{hook}", Scenario = unit.Name, File = unit.File, Detail = RunRecorder.Describe(error) });
        failed?.Invoke();
    }

    private IEnumerator EnterStart(RunUnit unit)
    {
        var start = unit.Start;
        Exception error = null;
        var startedAt = _host.Now;
        yield return GuardedRoutine.Run(_host.EnterStart(start, unit.LevelFolder), exception => error = exception);
        if (error == null)
            yield break;

        unit.StartFailed = true;
        if (error is not StartFailedException)
            _recorder.LogError(error);

        _recorder.Report(new RunStep
        {
            Name = $"start: {start.State.Name}",
            Scenario = unit.Name,
            File = unit.File,
            Line = (int)(start.Location?.Line ?? 0),
            Detail = error is StartFailedException ? error.Message : RunRecorder.Describe(error),
            Seconds = _recorder.Seconds(startedAt),
        });
        yield return _recorder.FailureScreenshot(unit);
    }

    private IEnumerator RunNodes(RunUnit unit, IReadOnlyList<ScenarioNode> nodes, Frame frame)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case StepNode step:
                    yield return RunStepNode(unit, step, frame);
                    break;
                case ForNode loop:
                    yield return RunFor(unit, loop, frame);
                    break;
                case RepeatNode repeat:
                    yield return RunRepeat(unit, repeat, frame);
                    break;
                case RunNode run:
                    yield return RunFile(unit, run, frame);
                    break;
            }

            if (unit.Stopped)
                yield break;
        }
    }

    private IEnumerator RunFor(RunUnit unit, ForNode loop, Frame frame)
    {
        foreach (var value in loop.Values)
        {
            var values = frame.Values.NewScope();
            var problem = ProblemDefining(() => values.Define(loop.Variable, new TextValue(value)));
            if (problem != null)
            {
                yield return FailNode(unit, frame, ControlNodes.For, loop.Location, problem);
                yield break;
            }

            yield return RunNodes(unit, loop.Steps, frame.Pass(values, $"{loop.Variable}={value}"));
            if (unit.Stopped)
                yield break;
        }
    }

    private IEnumerator RunRepeat(RunUnit unit, RepeatNode repeat, Frame frame)
    {
        for (var pass = 1; pass <= repeat.Count; pass++)
        {
            yield return RunNodes(unit, repeat.Steps, frame.Pass(frame.Values.NewScope(), $"repeat {pass}/{repeat.Count}"));
            if (unit.Stopped)
                yield break;
        }
    }

    private IEnumerator RunFile(RunUnit unit, RunNode run, Frame frame)
    {
        var values = new ScenarioValues();
        var problem = ProblemDefining(() =>
        {
            foreach (var entry in run.With)
                values.Define(entry.Key, new TextValue(WithNamesWritten(entry.Value, frame.Values)));
        });
        if (problem != null)
        {
            yield return FailNode(unit, frame, ControlNodes.Run, run.Location, problem);
            yield break;
        }

        var file = _loader.Loaded(ScenarioPaths.FromScenario(frame.File, run.File));
        yield return RunNodes(unit, file.Steps, frame.Into(file.File, values, run.Location));
    }

    /// <summary>
    /// What <paramref name="define"/> failed with, or null.
    /// </summary>
    private static string ProblemDefining(Action define)
    {
        try
        {
            define();
            return null;
        }
        catch (StepFailedException exception)
        {
            return exception.Message;
        }
    }

    private IEnumerator FailNode(RunUnit unit, Frame frame, string name, SourceLocation location, string problem)
    {
        unit.Stopped = true;
        _recorder.Report(frame.Failed(name, location, unit.Name, problem));
        yield return _recorder.FailureScreenshot(unit);
    }

    private static string WithNamesWritten(ScalarArgument value, ScenarioValues values)
    {
        try
        {
            return values.Substitute(value.Text);
        }
        catch (StepFailedException exception)
        {
            throw new StepFailedException($"{value.Location}: {exception.Message}");
        }
    }

    private IEnumerator RunStepNode(RunUnit unit, StepNode node, Frame frame)
    {
        var context = unit.Context.ForStep(_host, _exceptions, frame.Values);
        var recorded = new RunStep
        {
            Name = node.Step.FullName,
            Scenario = unit.Name,
            File = frame.File,
            Line = (int)node.Location.Line,
            RanFrom = frame.RanFrom,
            Passes = frame.Passes,
        };
        yield return _recorder.RunGuarded(context, BindAndRun(node, context, frame), recorded);
        _recorder.Report(recorded);

        if (context.Failed)
        {
            unit.Stopped = true;
            yield return _recorder.FailureScreenshot(unit);
        }
    }

    /// <summary>
    /// Each pass binds a new step from what the file wrote, so nothing is substituted twice.
    /// </summary>
    private IEnumerator BindAndRun(StepNode node, StepContext context, Frame frame)
    {
        var step = StepBinder.Bind(node, _steps.TypeOf(node.Step), frame.Values);
        yield return StepPreconditions.Run(step, context);
    }

    /// <summary>
    /// Where the running nodes are: their file, their scope and what led there.
    /// </summary>
    private sealed class Frame
    {
        private Frame(string file, ScenarioValues values, string ranFrom, string passes)
        {
            File = file;
            Values = values;
            RanFrom = ranFrom;
            Passes = passes;
        }

        public string File { get; }

        public ScenarioValues Values { get; }

        public string RanFrom { get; }

        public string Passes { get; }

        public static Frame Top(string file)
        {
            return new Frame(file, new ScenarioValues(), string.Empty, string.Empty);
        }

        public Frame Pass(ScenarioValues values, string pass)
        {
            return new Frame(File, values, RanFrom, Passes.Length == 0 ? pass : $"{Passes}, {pass}");
        }

        public Frame Into(string file, ScenarioValues values, SourceLocation calledAt)
        {
            var call = $"{calledAt.File}:{calledAt.Line}";
            return new Frame(file, values, RanFrom.Length == 0 ? call : $"{RanFrom} > {call}", Passes);
        }

        public RunStep Failed(string name, SourceLocation location, string scenario, string detail)
        {
            return new RunStep { Name = name, Scenario = scenario, File = File, Line = (int)location.Line, RanFrom = RanFrom, Passes = Passes, Detail = detail };
        }
    }
}
