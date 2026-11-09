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
        var units = LoadAll(files).Select(scenario => RunningScenario.Of(scenario, new ScenarioContext(run, ScenarioName(scenario.File)), RunScenarioSteps))
            .Concat(SelectTests(tests).Select(test => RunningScenario.Of(test, new ScenarioContext(run, test.Descriptor.Name), RunTest)))
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
            yield return RunScenario(unit, isFirst: i == 0);
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

    private void ReportNotRun(IEnumerable<RunningScenario> scenarios, string why)
    {
        foreach (var scenario in scenarios)
            _report(new RunStep { Name = "not run", Scenario = scenario.Name, File = scenario.File, Detail = why });
    }

    private IEnumerator RunScenario(RunningScenario scenario, bool isFirst)
    {
        _host.Logger.LogInfo($"{TestRun.Marker} scenario {scenario.Name}");
        _exceptions.StartScenario(scenario.Name);
        _host.Input.Route = scenario.InputRoute;

        var ready = true;
        if (!isFirst)
        {
            foreach (var hooks in Hooks)
                yield return RunHook(scenario, hooks, nameof(IScenarioHooks.BeforeScenario), () => hooks.BeforeScenario(scenario.Context), () => ready = false);
        }

        if (ready)
        {
            yield return EnterStart(scenario);
            if (!scenario.StartFailed)
                yield return scenario.RunBody();
        }

        foreach (var hooks in Hooks)
            yield return RunHook(scenario, hooks, nameof(IScenarioHooks.AfterScenario), () => hooks.AfterScenario(scenario.Context), null);
    }

    private IEnumerator RunHook(RunningScenario scenario, IScenarioHooks hooks, string hook, Func<IEnumerator> routine, Action failed)
    {
        Exception error = null;
        yield return GuardedRoutine.Run(Called(routine), exception => error = exception);
        if (error == null)
            yield break;

        LogError(error);
        _report(new RunStep { Name = $"{hooks.GetType().Name}.{hook}", Scenario = scenario.Name, File = scenario.File, Detail = Describe(error) });
        failed?.Invoke();
    }

    /// <summary>
    /// A hook or a test's class needn't be an iterator, so the call itself can throw too.
    /// </summary>
    private static IEnumerator Called(Func<IEnumerator> routine)
    {
        yield return routine();
    }

    private IEnumerator EnterStart(RunningScenario scenario)
    {
        var start = scenario.Start;
        Exception error = null;
        var startedAt = _host.Now;
        yield return GuardedRoutine.Run(_host.EnterStart(start, scenario.File), exception => error = exception);
        if (error == null)
            yield break;

        scenario.StartFailed = true;
        if (error is not StartFailedException)
            LogError(error);

        _report(new RunStep
        {
            Name = $"start: {start.State.Name}",
            Scenario = scenario.Name,
            File = scenario.File,
            Line = (int)(start.Location?.Line ?? 0),
            Detail = error is StartFailedException ? error.Message : Describe(error),
            Seconds = Seconds(startedAt),
        });
        yield return FailureScreenshot(scenario);
    }

    private IEnumerator RunScenarioSteps(RunningScenario scenario, Scenario file)
    {
        return RunNodes(scenario, file.Steps, Frame.Top(file.File));
    }

    /// <summary>
    /// The test's steps record themselves as <see cref="IStepContext.Run"/> runs them. The test's own entry, <c>test</c>, is
    /// there when the test failed outside a step, recorded a check of its own, or ran no step, so every test shows. A failed step
    /// that stops the test doesn't hide what the test recorded before it.
    /// </summary>
    private IEnumerator RunTest(RunningScenario scenario, InGameTest test)
    {
        var values = new ScenarioValues();
        var stepsRun = 0;
        var context = scenario.Context.ForStep(_host, _exceptions, values, step => RunTestStep(scenario, values, step, () => stepsRun++));
        var startedAt = _host.Now;
        Exception error = null;
        yield return GuardedRoutine.Run(Called(() => test.Run(context)), exception => error = exception);
        var stopped = error is TestStoppedException;
        if (error != null && !stopped)
        {
            if (error is not StepFailedException)
                LogError(error);

            context.Fail(Describe(error));
        }

        if (!context.Failed && stepsRun > 0 && context.Detail.Length == 0 && context.Expected == null)
            yield break;

        _report(new RunStep
        {
            Name = "test",
            Passed = !context.Failed,
            Detail = context.Detail,
            Expected = context.Expected,
            Actual = context.Actual,
            Scenario = scenario.Name,
            Seconds = Seconds(startedAt),
        });

        // The step that stopped it took one already
        if (context.Failed && !stopped)
            yield return FailureScreenshot(scenario);
    }

    /// <exception cref="TestStoppedException">The step failed, which stops the test that ran it.</exception>
    private IEnumerator RunTestStep(RunningScenario scenario, ScenarioValues values, Step step, Action ran)
    {
        var context = scenario.Context.ForStep(_host, _exceptions, values);
        var startedAt = _host.Now;
        Exception error = null;
        yield return GuardedRoutine.Run(StepPreconditions.Run(step, context), exception => error = exception);
        if (error != null)
        {
            if (error is not StepFailedException)
                LogError(error);

            context.Fail(Describe(error));
        }

        ran();
        _report(new RunStep
        {
            Name = step == null ? "null" : _steps.NameOf(step.GetType()),
            Passed = !context.Failed,
            Detail = context.Detail,
            Expected = context.Expected,
            Actual = context.Actual,
            Scenario = scenario.Name,
            Seconds = Seconds(startedAt),
        });

        if (!context.Failed)
            yield break;

        yield return FailureScreenshot(scenario);
        throw new TestStoppedException();
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
            var problem = ProblemDefining(() => values.Define(loop.Variable, new TextValue(value)));
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
        var problem = ProblemDefining(() =>
        {
            foreach (var entry in run.With)
                values.Define(entry.Key, new TextValue(WithNamesWritten(entry.Value, frame.Values)));
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

    private IEnumerator FailNode(RunningScenario scenario, Frame frame, string name, SourceLocation location, string problem)
    {
        scenario.Stopped = true;
        _report(frame.Failed(name, location, scenario.Name, problem));
        yield return FailureScreenshot(scenario);
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

    private IEnumerator RunStepNode(RunningScenario scenario, StepNode node, Frame frame)
    {
        var context = scenario.Context.ForStep(_host, _exceptions, frame.Values);
        var startedAt = _host.Now;
        Exception error = null;
        yield return GuardedRoutine.Run(BindAndRun(node, context, frame), exception => error = exception);
        if (error != null)
        {
            if (error is not StepFailedException)
                LogError(error);

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
        yield return GuardedRoutine.Run(_host.Screenshot(ScreenshotNames.For(scenario.Name, "failure")), LogError);
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
    private sealed class RunningScenario
    {
        private readonly Func<RunningScenario, IEnumerator> _body;

        private RunningScenario(ScenarioContext context, string file, ScenarioStart start, InputRoute inputRoute, Func<RunningScenario, IEnumerator> body)
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

        public static RunningScenario Of(Scenario scenario, ScenarioContext context, Func<RunningScenario, Scenario, IEnumerator> run)
        {
            return new RunningScenario(context, scenario.File, scenario.Start, scenario.Input, running => run(running, scenario));
        }

        public static RunningScenario Of(InGameTest test, ScenarioContext context, Func<RunningScenario, InGameTest, IEnumerator> run)
        {
            var start = new ScenarioStart(StartState.All.Single(state => state.Name == test.Descriptor.Start), new Dictionary<string, string>());
            return new RunningScenario(context, string.Empty, start, InputRoute.InProcess, running => run(running, test));
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
