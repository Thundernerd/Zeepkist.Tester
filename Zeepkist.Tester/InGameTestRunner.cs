using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Selects the C# tests a run names, and runs each as a <see cref="RunUnit"/>. A failed step stops its test, as one stops a
/// scenario.
/// </summary>
internal sealed class InGameTestRunner
{
    private readonly IRunHost _host;
    private readonly ExceptionLog _exceptions;
    private readonly RunCatalog _catalog;
    private readonly RunRecorder _recorder;
    private readonly string _scenariosFolder;

    /// <param name="host">The game.</param>
    /// <param name="exceptions">Where the game's exceptions are listed under each test.</param>
    /// <param name="catalog">The tests to choose from, and the steps they run.</param>
    /// <param name="recorder">Records each step.</param>
    /// <param name="scenariosFolder">What a test's <see cref="InGameTestAttribute.Level"/> is relative to.</param>
    public InGameTestRunner(IRunHost host, ExceptionLog exceptions, RunCatalog catalog, RunRecorder recorder, string scenariosFolder)
    {
        _scenariosFolder = scenariosFolder;
        _host = host;
        _exceptions = exceptions;
        _catalog = catalog;
        _recorder = recorder;
    }

    /// <summary>
    /// The tests <paramref name="selectors"/> name, ready to run. A selector that names no test is reported.
    /// </summary>
    /// <param name="selectors">Each a test's name, or its class's for each test in it.</param>
    /// <param name="run">The run they're part of.</param>
    public List<RunUnit> Select(IReadOnlyList<string> selectors, RunContext run)
    {
        var selected = _catalog.Tests.Select(selectors, out var unknown);
        foreach (var selector in unknown)
            _recorder.Report(new RunStep { Name = "load", Scenario = selector, Detail = $"No [InGameTest] is named {selector}: a test is selected by its class's name, or by Class.Method." });

        return [.. selected.Select(test => UnitOf(test, run))];
    }

    private RunUnit UnitOf(InGameTest test, RunContext run)
    {
        return new RunUnit(new ScenarioContext(run, test.Descriptor.Name), string.Empty, StartOf(test.Descriptor), _scenariosFolder, InputRoute.InProcess, unit => RunTest(unit, test));
    }

    /// <summary>
    /// The test's start as a scenario's <c>start:</c> would say it, with its level file or free play's UID.
    /// </summary>
    private static ScenarioStart StartOf(TestDescriptor test)
    {
        var state = StartState.All.Single(state => state.Name == test.Start);
        var arguments = new Dictionary<string, string>();
        var argument = state == StartState.FreePlay ? test.FreePlayUid : test.Level;
        if (state.Argument != null && argument != null)
            arguments.Add(state.Argument, argument);

        return new ScenarioStart(state, arguments);
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
        var context = unit.Context.ForTest(_host, _exceptions, values, step => RunTestStep(unit, values, step, () => stepsRun++));
        var recorded = new RunStep { Name = "test", Scenario = unit.Name };
        var stopped = false;
        yield return _recorder.RunGuarded(context, RunRecorder.Called(() => test.Run(context)), recorded, error => stopped = error is TestStoppedException);
        if (!context.Failed && stepsRun > 0 && context.Detail.Length == 0 && context.Expected == null)
            yield break;

        _recorder.Report(recorded);

        // The step that stopped it took one already
        if (context.Failed && !stopped)
            yield return _recorder.FailureScreenshot(unit);
    }

    /// <exception cref="TestStoppedException">The step failed, which stops the test that ran it.</exception>
    private IEnumerator RunTestStep(RunUnit unit, ScenarioValues values, Step step, Action ran)
    {
        var context = unit.Context.ForStep(_host, _exceptions, values);
        var recorded = new RunStep { Name = step == null ? "null" : _catalog.Steps.NameOf(step.GetType()), Scenario = unit.Name };
        yield return _recorder.RunGuarded(context, StepPreconditions.Run(step, context), recorded);
        ran();
        _recorder.Report(recorded);
        if (!context.Failed)
            yield break;

        yield return _recorder.FailureScreenshot(unit);
        throw new TestStoppedException();
    }

    /// <summary>
    /// Thrown out of a C# test by the step that failed, to stop it as a failed step stops a scenario.
    /// </summary>
    private sealed class TestStoppedException : Exception
    {
    }
}
