using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Runs scenarios in order. A failed step stops its scenario with a screenshot, and the run goes on with the next. A start
/// that fails ends the run, because that leaves the game somewhere unknown, and what follows there would say nothing.
/// </summary>
internal sealed class ScenarioRunner
{
    private const string ScenarioFileEnd = ".ztest.yaml";

    private readonly IRunHost _host;
    private readonly ExceptionLog _exceptions;
    private readonly StepTypes _steps;
    private readonly IReadOnlyList<TestPluginRegistration> _plugins;
    private readonly Action<RunStep> _report;
    private readonly ScenarioLoader _loader;

    public ScenarioRunner(IRunHost host, ExceptionLog exceptions, StepTypes steps, IReadOnlyList<TestPluginRegistration> plugins, Action<RunStep> report)
    {
        _host = host;
        _exceptions = exceptions;
        _steps = steps;
        _plugins = plugins;
        _report = report;
        _loader = new ScenarioLoader(steps.Descriptors);
    }

    private IEnumerable<IScenarioHooks> Hooks => _plugins.SelectMany(plugin => plugin.ScenarioHooks);

    public IEnumerator Run(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            _report(RunStep.Fail("scenarios", "run.json names no scenarios, so this run tested nothing."));
            yield break;
        }

        var run = new RunContext(_host.Logger, Services());
        var scenarios = LoadAll(files);
        if (scenarios.Count == 0)
            yield break;

        if (!RunStartedHooks(run, out var problem))
        {
            ReportNotRun(scenarios, problem);
            yield break;
        }

        for (var i = 0; i < scenarios.Count; i++)
        {
            var scenario = new RunningScenario(scenarios[i], new ScenarioContext(run, NameOf(scenarios[i].File)));
            yield return RunScenario(scenario, isFirst: i == 0);
            if (scenario.StartFailed)
            {
                ReportNotRun(scenarios.Skip(i + 1), $"{scenario.Name} couldn't get to where it starts, so the game is somewhere unknown.");
                yield break;
            }
        }
    }

    private static string NameOf(string file)
    {
        var name = Path.GetFileName(file);
        return name.EndsWith(ScenarioFileEnd, StringComparison.OrdinalIgnoreCase)
            ? name.Substring(0, name.Length - ScenarioFileEnd.Length)
            : Path.GetFileNameWithoutExtension(name);
    }

    private static string Describe(Exception exception)
    {
        return exception is StepFailedException ? exception.Message : $"{exception.GetType().Name}: {exception.Message}";
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
                _report(new RunStep { Name = "load", Scenario = NameOf(file), File = exception.Location.File, Line = (int)exception.Location.Line, Detail = exception.Message });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _report(new RunStep { Name = "load", Scenario = NameOf(file), File = file, Detail = exception.Message });
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
                _host.Logger.LogError($"{TestRun.Marker} {exception}");
                _report(RunStep.Fail(name, Describe(exception)));
                problem = $"{name} failed.";
                return false;
            }
        }

        problem = null;
        return true;
    }

    private void ReportNotRun(IEnumerable<Scenario> scenarios, string why)
    {
        foreach (var scenario in scenarios)
            _report(new RunStep { Name = "not run", Scenario = NameOf(scenario.File), File = scenario.File, Detail = why });
    }

    private IEnumerator RunScenario(RunningScenario scenario, bool isFirst)
    {
        _host.Logger.LogInfo($"{TestRun.Marker} scenario {scenario.Name}");
        _exceptions.StartScenario(scenario.Name);

        var ready = true;
        if (!isFirst)
        {
            foreach (var hooks in Hooks)
                yield return RunHook(scenario, hooks, nameof(IScenarioHooks.BeforeScenario), hooks.BeforeScenario(scenario.Context), () => ready = false);
        }

        if (ready)
        {
            yield return EnterStart(scenario);
            if (!scenario.StartFailed)
                yield return RunNodes(scenario, scenario.Scenario.Steps, Frame.Top(scenario.Scenario.File));
        }

        foreach (var hooks in Hooks)
            yield return RunHook(scenario, hooks, nameof(IScenarioHooks.AfterScenario), hooks.AfterScenario(scenario.Context), null);
    }

    private IEnumerator RunHook(RunningScenario scenario, IScenarioHooks hooks, string hook, IEnumerator routine, Action failed)
    {
        Exception error = null;
        yield return GuardedRoutine.Run(routine, exception => error = exception);
        if (error == null)
            yield break;

        _host.Logger.LogError($"{TestRun.Marker} {error}");
        _report(new RunStep { Name = $"{hooks.GetType().Name}.{hook}", Scenario = scenario.Name, File = scenario.Scenario.File, Detail = Describe(error) });
        failed?.Invoke();
    }

    private IEnumerator EnterStart(RunningScenario scenario)
    {
        var start = scenario.Scenario.Start;
        Exception error = null;
        var startedAt = _host.Now;
        yield return GuardedRoutine.Run(_host.EnterStart(start, scenario.Scenario.File), exception => error = exception);
        if (error == null)
            yield break;

        scenario.StartFailed = true;
        if (error is not StartFailedException)
            _host.Logger.LogError($"{TestRun.Marker} {error}");

        _report(new RunStep
        {
            Name = $"start: {start.State.Name}",
            Scenario = scenario.Name,
            File = scenario.Scenario.File,
            Line = (int)(start.Location?.Line ?? 0),
            Detail = error is StartFailedException ? error.Message : Describe(error),
            Seconds = Seconds(startedAt),
        });
        yield return FailureScreenshot(scenario);
    }

    private IEnumerator RunNodes(RunningScenario scenario, IReadOnlyList<ScenarioNode> nodes, Frame frame)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case StepNode step:
                    yield return RunStepNode(scenario, step, frame);
                    break;
                case ForNode loop:
                    yield return RunFor(scenario, loop, frame);
                    break;
                case RepeatNode repeat:
                    yield return RunRepeat(scenario, repeat, frame);
                    break;
                case RunNode run:
                    yield return RunFile(scenario, run, frame);
                    break;
            }

            if (scenario.Stopped)
                yield break;
        }
    }

    private IEnumerator RunFor(RunningScenario scenario, ForNode loop, Frame frame)
    {
        foreach (var value in loop.Values)
        {
            var values = frame.Values.NewScope();
            var problem = Problem(() => values.Define(loop.Variable, new TextValue(value)));
            if (problem != null)
            {
                yield return FailNode(scenario, frame, "for", loop.Location, problem);
                yield break;
            }

            yield return RunNodes(scenario, loop.Steps, frame.Pass(values, $"{loop.Variable}={value}"));
            if (scenario.Stopped)
                yield break;
        }
    }

    private IEnumerator RunRepeat(RunningScenario scenario, RepeatNode repeat, Frame frame)
    {
        for (var pass = 1; pass <= repeat.Count; pass++)
        {
            yield return RunNodes(scenario, repeat.Steps, frame.Pass(frame.Values.NewScope(), $"repeat {pass}/{repeat.Count}"));
            if (scenario.Stopped)
                yield break;
        }
    }

    private IEnumerator RunFile(RunningScenario scenario, RunNode run, Frame frame)
    {
        var values = new ScenarioValues();
        var problem = Problem(() =>
        {
            foreach (var entry in run.With)
                values.Define(entry.Key, new TextValue(Written(entry.Value, frame.Values)));
        });
        if (problem != null)
        {
            yield return FailNode(scenario, frame, "run", run.Location, problem);
            yield break;
        }

        var file = _loader.Loaded(ScenarioPaths.FromScenario(frame.File, run.File));
        yield return RunNodes(scenario, file.Steps, frame.Into(file.File, values, run.Location));
    }

    /// <summary>
    /// What <paramref name="define"/> failed with, or null.
    /// </summary>
    private static string Problem(Action define)
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

    private IEnumerator FailNode(RunningScenario scenario, Frame frame, string name, SourceLocation location, string problem)
    {
        scenario.Stopped = true;
        _report(frame.Failed(name, location, scenario.Name, problem));
        yield return FailureScreenshot(scenario);
    }

    private static string Written(ScalarArgument value, ScenarioValues values)
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

    private IEnumerator RunStepNode(RunningScenario scenario, StepNode node, Frame frame)
    {
        var context = scenario.Context.ForStep(_host, _exceptions, frame.Values);
        var startedAt = _host.Now;
        Exception error = null;
        yield return GuardedRoutine.Run(BindAndRun(node, context, frame), exception => error = exception);
        if (error != null)
        {
            if (error is not StepFailedException)
                _host.Logger.LogError($"{TestRun.Marker} {error}");

            context.Fail(Describe(error));
        }

        _report(new RunStep
        {
            Name = node.Step.FullName,
            Passed = !context.Failed,
            Detail = context.Detail,
            Expected = context.Expected,
            Actual = context.Actual,
            Scenario = scenario.Name,
            File = frame.File,
            Line = (int)node.Location.Line,
            RanFrom = frame.RanFrom,
            Passes = frame.Passes,
            Seconds = Seconds(startedAt),
        });

        if (context.Failed)
        {
            scenario.Stopped = true;
            yield return FailureScreenshot(scenario);
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
    /// Not being able to take one shouldn't change what the run found.
    /// </summary>
    private IEnumerator FailureScreenshot(RunningScenario scenario)
    {
        yield return GuardedRoutine.Run(_host.Screenshot(ScreenshotNames.For(scenario.Name, "failure")), exception => _host.Logger.LogError($"{TestRun.Marker} {exception}"));
    }

    private double Seconds(float startedAt)
    {
        return Math.Round(_host.Now - startedAt, 2);
    }

    private sealed class RunningScenario
    {
        public RunningScenario(Scenario scenario, ScenarioContext context)
        {
            Scenario = scenario;
            Context = context;
        }

        public Scenario Scenario { get; }

        public ScenarioContext Context { get; }

        public string Name => Context.ScenarioName;

        public bool Stopped { get; set; }

        public bool StartFailed { get; set; }
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
