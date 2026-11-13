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
    private const string ScenarioFileEnd = ".ztest.yaml";

    private readonly IRunHost _host;
    private readonly ExceptionLog _exceptions;
    private readonly StepTypes _steps;
    private readonly IReadOnlyList<TestPluginRegistration> _plugins;
    private readonly Action<RunStep> _report;
    private readonly ScenarioLoader _loader;
    private readonly string _folder;
    private readonly InGameTests _tests;

    /// <param name="host">The game.</param>
    /// <param name="exceptions">Where the game's exceptions are listed under each scenario.</param>
    /// <param name="steps">The steps scenarios can use.</param>
    /// <param name="plugins">What the test plugins gave.</param>
    /// <param name="report">Takes each step's result.</param>
    /// <param name="folder">What a scenario's name is relative to, so two files of one name in two folders stay apart.</param>
    /// <param name="tests">The C# tests a run can choose from.</param>
    public ScenarioRunner(IRunHost host, ExceptionLog exceptions, StepTypes steps, IReadOnlyList<TestPluginRegistration> plugins, Action<RunStep> report, string folder, InGameTests tests)
    {
        _folder = folder;
        _host = host;
        _exceptions = exceptions;
        _steps = steps;
        _plugins = plugins;
        _report = report;
        _tests = tests;
        _loader = new ScenarioLoader(steps.Descriptors);
    }

    private IEnumerable<IScenarioHooks> Hooks => _plugins.SelectMany(plugin => plugin.ScenarioHooks);

    /// <param name="files">The scenario files to run, in order.</param>
    /// <param name="tests">The C# tests to run after them: each a test's name, or its class's for each test in it.</param>
    public IEnumerator Run(IReadOnlyList<string> files, IReadOnlyList<string> tests)
    {
        if (files.Count == 0 && tests.Count == 0)
        {
            _report(RunStep.Fail("scenarios", "run.json names no scenarios or tests, so this run tested nothing."));
            yield break;
        }

        var run = new RunContext(_host.Logger, Services());
        var units = LoadAll(files).Select(scenario => RunUnit.Of(scenario, new ScenarioContext(run, ScenarioName(scenario.File)), RunScenarioSteps))
            .Concat(SelectTests(tests).Select(test => RunUnit.Of(test, new ScenarioContext(run, test.Descriptor.Name), RunTest)))
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

    private List<InGameTest> SelectTests(IReadOnlyList<string> selectors)
    {
        var selected = _tests.Select(selectors, out var unknown);
        foreach (var selector in unknown)
            _report(new RunStep { Name = "load", Scenario = selector, Detail = $"No [InGameTest] is named {selector}: a test is selected by its class's name, or by Class.Method." });

        return selected;
    }

    private string ScenarioName(string file)
    {
        var name = (file.StartsWith(_folder, StringComparison.Ordinal) ? file.Substring(_folder.Length) : Path.GetFileName(file))
            .Replace('\\', '/')
            .TrimStart('/');
        return name.EndsWith(ScenarioFileEnd, StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - ScenarioFileEnd.Length) : name;
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
                _report(new RunStep { Name = "load", Scenario = ScenarioName(file), File = exception.Location.File, Line = (int)exception.Location.Line, Detail = exception.Message });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _report(new RunStep { Name = "load", Scenario = ScenarioName(file), File = file, Detail = exception.Message });
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
                LogError(exception);
                _report(RunStep.Fail(name, Describe(exception)));
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
            _report(new RunStep { Name = "not run", Scenario = unit.Name, File = unit.File, Detail = why });
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
        yield return GuardedRoutine.Run(Called(routine), exception => error = exception);
        if (error == null)
            yield break;

        LogError(error);
        _report(new RunStep { Name = $"{hooks.GetType().Name}.{hook}", Scenario = unit.Name, File = unit.File, Detail = Describe(error) });
        failed?.Invoke();
    }

    /// <summary>
    /// A hook or a test's class needn't be an iterator, so the call itself can throw too.
    /// </summary>
    private static IEnumerator Called(Func<IEnumerator> routine)
    {
        yield return routine();
    }

    private IEnumerator EnterStart(RunUnit unit)
    {
        var start = unit.Start;
        Exception error = null;
        var startedAt = _host.Now;
        yield return GuardedRoutine.Run(_host.EnterStart(start, unit.File), exception => error = exception);
        if (error == null)
            yield break;

        unit.StartFailed = true;
        if (error is not StartFailedException)
            LogError(error);

        _report(new RunStep
        {
            Name = $"start: {start.State.Name}",
            Scenario = unit.Name,
            File = unit.File,
            Line = (int)(start.Location?.Line ?? 0),
            Detail = error is StartFailedException ? error.Message : Describe(error),
            Seconds = Seconds(startedAt),
        });
        yield return FailureScreenshot(unit);
    }

    private IEnumerator RunScenarioSteps(RunUnit unit, Scenario file)
    {
        return RunNodes(unit, file.Steps, Frame.Top(file.File));
    }

    /// <summary>
    /// The test's steps record themselves as <see cref="IStepContext.Run"/> runs them. The test's own entry, <c>test</c>, is
    /// there when the test failed outside a step, recorded a check of its own, or ran no step, so every test shows. A failed step
    /// that stops the test doesn't hide what the test recorded before it.
    /// </summary>
    private IEnumerator RunTest(RunUnit unit, InGameTest test)
    {
        var values = new ScenarioValues();
        var stepsRun = 0;
        var stopped = false;
        var context = unit.Context.ForTest(_host, _exceptions, values, step => RunTestStep(unit, values, step, () => stepsRun++, () => stopped = true));
        RunStep recorded = null;
        yield return RunRecorded(context, Called(() => test.Run(context)), step => recorded = step, error => error is TestStoppedException);
        if (!context.Failed && stepsRun > 0 && context.Detail.Length == 0 && context.Expected == null)
            yield break;

        recorded.Name = "test";
        recorded.Scenario = unit.Name;
        _report(recorded);

        // The step that stopped it took one already
        if (context.Failed && !stopped)
            yield return FailureScreenshot(unit);
    }

    /// <exception cref="TestStoppedException">The step failed, which stops the test that ran it.</exception>
    private IEnumerator RunTestStep(RunUnit unit, ScenarioValues values, Step step, Action ran, Action stopping)
    {
        var context = unit.Context.ForStep(_host, _exceptions, values);
        RunStep recorded = null;
        yield return RunRecorded(context, StepPreconditions.Run(step, context), result => recorded = result);
        ran();
        recorded.Name = step == null ? "null" : _steps.NameOf(step.GetType());
        recorded.Scenario = unit.Name;
        _report(recorded);
        if (!context.Failed)
            yield break;

        yield return FailureScreenshot(unit);
        stopping();
        throw new TestStoppedException();
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
        _report(frame.Failed(name, location, unit.Name, problem));
        yield return FailureScreenshot(unit);
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
        RunStep recorded = null;
        yield return RunRecorded(context, BindAndRun(node, context, frame), step => recorded = step);
        recorded.Name = node.Step.FullName;
        recorded.Scenario = unit.Name;
        recorded.File = frame.File;
        recorded.Line = (int)node.Location.Line;
        recorded.RanFrom = frame.RanFrom;
        recorded.Passes = frame.Passes;
        _report(recorded);

        if (context.Failed)
        {
            unit.Stopped = true;
            yield return FailureScreenshot(unit);
        }
    }

    /// <summary>
    /// Runs <paramref name="routine"/> for <paramref name="context"/>'s step, failing the step with what it throws, then gives
    /// <paramref name="recorded"/> the step's result, for the caller to say which step it was. An exception
    /// <paramref name="stops"/> holds for only stops the routine, which fails nothing.
    /// </summary>
    private IEnumerator RunRecorded(StepContext context, IEnumerator routine, Action<RunStep> recorded, Func<Exception, bool> stops = null)
    {
        var startedAt = _host.Now;
        Exception error = null;
        yield return GuardedRoutine.Run(routine, exception => error = exception);
        if (error != null && stops?.Invoke(error) != true)
        {
            if (error is not StepFailedException)
                LogError(error);

            context.Fail(Describe(error));
        }

        recorded(new RunStep
        {
            Passed = !context.Failed,
            Detail = context.Detail,
            Expected = context.Expected,
            Actual = context.Actual,
            Seconds = Seconds(startedAt),
        });
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
    private IEnumerator FailureScreenshot(RunUnit unit)
    {
        yield return GuardedRoutine.Run(_host.Screenshot(ScreenshotNames.For(unit.Name, "failure")), LogError);
    }

    private void LogError(Exception exception)
    {
        _host.Logger.LogError($"{TestRun.Marker} {exception}");
    }

    private double Seconds(float startedAt)
    {
        return Math.Round(_host.Now - startedAt, 2);
    }

    /// <summary>
    /// A scenario or a C# test, which run alike: hooks around them, from where they start, and their steps.
    /// </summary>
    private sealed class RunUnit
    {
        private readonly Func<RunUnit, IEnumerator> _body;

        private RunUnit(ScenarioContext context, string file, ScenarioStart start, InputRoute inputRoute, Func<RunUnit, IEnumerator> body)
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

        public static RunUnit Of(Scenario scenario, ScenarioContext context, Func<RunUnit, Scenario, IEnumerator> run)
        {
            return new RunUnit(context, scenario.File, scenario.Start, scenario.Input, running => run(running, scenario));
        }

        public static RunUnit Of(InGameTest test, ScenarioContext context, Func<RunUnit, InGameTest, IEnumerator> run)
        {
            var start = new ScenarioStart(StartState.All.Single(state => state.Name == test.Descriptor.Start), new Dictionary<string, string>());
            return new RunUnit(context, string.Empty, start, InputRoute.InProcess, running => run(running, test));
        }

        public IEnumerator RunBody()
        {
            return _body(this);
        }
    }

    /// <summary>
    /// Thrown out of a C# test by the step that failed, to stop it as a failed step stops a scenario.
    /// </summary>
    private sealed class TestStoppedException : Exception
    {
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
