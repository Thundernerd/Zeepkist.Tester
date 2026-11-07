using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Steps;
using Xunit;
using static TNRD.Zeepkist.Tester.Tests.RunnerSteps;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// What a run does with scenario files, against a game that only says what it was asked to do.
/// </summary>
public sealed class ScenarioRunnerTests : IDisposable
{
    private static readonly Type[] _stepTypes =
    [
        .. typeof(RunnerSteps).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(Step))),
        typeof(Wait),
        typeof(Log),
        typeof(Screenshot),
    ];

    private readonly TemporaryFolder _folder = new TemporaryFolder();
    private readonly Recorder _recorder = new Recorder();
    private readonly ExceptionLog _exceptions = new ExceptionLog();
    private readonly List<RunStep> _steps = [];
    private readonly FakeHost _host;
    private readonly List<TestPluginRegistration> _plugins = [];

    public ScenarioRunnerTests()
    {
        _host = new FakeHost(_recorder);
        _plugins.Add(new TestPluginRegistration("the test plugin").Service(_recorder));
    }

    private List<string> Events => _recorder.Events;

    public void Dispose()
    {
        _folder.Dispose();
    }

    [Fact]
    public void NoScenarios_IsAFailedStepThatSaysSo()
    {
        Run();

        var step = Assert.Single(_steps);
        Assert.False(step.Passed);
        Assert.Equal("scenarios", step.Name);
        Assert.Equal("run.json names no scenarios, so this run tested nothing.", step.Detail);
    }

    [Fact]
    public void NoScenarios_FailsTheRun_WithTheRightBuilds()
    {
        var results = new RunResults("abc", _folder.Path, []);
        results.Add(RunStep.Pass("right build: a mod"));
        Run();
        foreach (var step in _steps)
            results.Add(step);

        results.Finish();

        Assert.True(results.Done);
        Assert.False(results.Passed);
    }

    [Fact]
    public void ScenariosRunInOrder_EachFromItsStart_RecordingEachStep()
    {
        var first = Write("first", "start: main-menu\nsteps:\n  - t.note: a\n");
        var second = Write("second", "steps:\n  - t.note: b\n  - t.check\n");

        Run(first, second);

        Assert.Equal(["enter main-menu", "a", "enter editor", "b"], Events);
        Assert.Equal(["first:3 t.note", "second:2 t.note", "second:3 t.check"], _steps.Select(step => $"{step.Scenario}:{step.Line} {step.Name}"));
        Assert.All(_steps, step => Assert.True(step.Passed));
        Assert.Equal(second, _steps[2].File);
        Assert.Equal("looks right", _steps[2].Detail);
        Assert.Equal("3", _steps[2].Expected);
        Assert.Equal("3", _steps[2].Actual);
    }

    [Fact]
    public void AFailedStep_StopsItsScenarioWithAScreenshot_AndTheRunGoesOn()
    {
        var failing = Write("failing", "steps:\n  - t.note: before\n  - t.fail: why\n  - t.note: after\n");
        var next = Write("next", "steps:\n  - t.note: next\n");

        Run(failing, next);

        Assert.Equal(["enter editor", "before", "screenshot failing-failure", "enter editor", "next"], Events);
        var step = _steps[1];
        Assert.False(step.Passed);
        Assert.Equal("why", step.Detail);
        Assert.Equal("expected", step.Expected);
        Assert.Equal("actual", step.Actual);
        Assert.True(_steps[2].Passed);
    }

    [Fact]
    public void TheFirstFailure_IsTheOneRecorded_AndTheStepRunsToItsEnd()
    {
        Run(Write("a", "steps:\n  - t.fail-twice\n"));

        Assert.Contains("went on after failing", Events);
        Assert.Equal("first", Assert.Single(_steps).Detail);
    }

    [Theory]
    [InlineData("t.throw", "InvalidOperationException: boom")]
    [InlineData("t.fail-by-exception", "stopped")]
    public void AnExceptionFromAStep_FailsIt(string step, string detail)
    {
        Run(Write("a", $"steps:\n  - {step}\n  - t.note: after\n"));

        var recorded = Assert.Single(_steps);
        Assert.False(recorded.Passed);
        Assert.Equal(detail, recorded.Detail);
        Assert.DoesNotContain("after", Events);
    }

    [Fact]
    public void AWaitThatGivesUp_StopsTheStep_SayingWhatItWaitedFor()
    {
        Run(Write("a", "steps:\n  - t.wait-for-nothing\n"));

        Assert.Equal("Waited 2 seconds for the door.", Assert.Single(_steps).Detail);
        Assert.DoesNotContain("went on after giving up", Events);
    }

    [Fact]
    public void Waits_FinishOnThisClock()
    {
        Run(Write("a", "steps:\n  - t.wait-a-while\n"));

        Assert.Contains("waited", Events);
        Assert.True(Assert.Single(_steps).Passed);
    }

    [Fact]
    public void ARequiredStateThatDoesntHold_FailsTheStepWithoutRunningIt()
    {
        Run(Write("a", "steps:\n  - t.needs-a-state\n"));

        Assert.Equal("t.needs-a-state needs t.never: Add t.note first.", Assert.Single(_steps).Detail);
        Assert.DoesNotContain("ran without its state", Events);
    }

    [Fact]
    public void RequireEditor_OutsideTheEditor_FailsTheStep()
    {
        Run(Write("a", "steps:\n  - t.needs-the-editor\n"));

        Assert.False(Assert.Single(_steps).Passed);
        Assert.DoesNotContain("ran without the editor", Events);
    }

    [Fact]
    public void AForLoop_RunsItsBodyPerValue_WithTheVariableInAnyTypeOfValue()
    {
        Run(Write("a", "steps:\n  - for: { h: [64, 120] }\n    steps:\n      - t.number: ${h}\n      - t.note: h is ${h}\n"));

        Assert.Equal(["enter editor", "number 64", "h is 64", "number 120", "h is 120"], Events);
        Assert.Equal(["h=64", "h=64", "h=120", "h=120"], _steps.Select(step => step.Passes));
    }

    [Fact]
    public void AForValue_ThatDoesntFit_FailsAtLoadBeforeAnythingRuns()
    {
        Run(Write("a", "steps:\n  - for: { h: [64, soon] }\n    steps:\n      - t.number: ${h}\n"));

        var step = Assert.Single(_steps);
        Assert.Equal("load", step.Name);
        Assert.Equal(4, step.Line);
        Assert.Contains("takes a whole number, not 'soon'", step.Detail);
        Assert.Empty(Events);
    }

    [Fact]
    public void ARepeat_GivesEachPassANewScope_SoANameCanBeDefinedEachTime()
    {
        Run(Write("a", "steps:\n  - repeat: 2\n    steps:\n      - t.define: lamp\n      - t.use: lamp\n"));

        Assert.All(_steps, step => Assert.True(step.Passed));
        Assert.Equal(["repeat 1/2", "repeat 1/2", "repeat 2/2", "repeat 2/2"], _steps.Select(step => step.Passes));
    }

    [Fact]
    public void AName_FromABody_IsGoneAfterIt()
    {
        Run(Write("a", "steps:\n  - repeat: 1\n    steps:\n      - t.define: lamp\n  - t.use: lamp\n"));

        Assert.Equal("The name 'lamp' isn't defined by an earlier step.", _steps.Last().Detail);
    }

    [Fact]
    public void ABody_CantDefineANameFromAroundIt()
    {
        Run(Write("a", "steps:\n  - t.define: lamp\n  - repeat: 1\n    steps:\n      - t.define: lamp\n"));

        Assert.Equal("The name 'lamp' is already defined, as a thing.", _steps.Last().Detail);
    }

    [Fact]
    public void AForVariable_ThatIsAlreadyDefined_FailsAtTheFor()
    {
        Run(Write("a", "steps:\n  - t.define-text: h\n  - for: { h: [1] }\n    steps:\n      - t.note: never\n"));

        var step = _steps.Last();
        Assert.Equal("for", step.Name);
        Assert.Equal(3, step.Line);
        Assert.Equal("The name 'h' is already defined, as a text.", step.Detail);
        Assert.DoesNotContain("never", Events);
    }

    [Fact]
    public void AValueNamingAnotherValue_IsSubstitutedOnce()
    {
        Run(Write("a", "steps:\n  - t.define: lamp\n  - for: { b: [lamp] }\n    steps:\n      - t.use: ${b}\n      - t.note: $${b}\n"));

        Assert.All(_steps, step => Assert.True(step.Passed, step.Detail));
        Assert.Contains("${b}", Events);
    }

    [Fact]
    public void AFailureInABody_StopsTheOtherPassesAndTheScenario()
    {
        Run(Write("a", "steps:\n  - repeat: 3\n    steps:\n      - t.note: pass\n      - t.fail: why\n  - t.note: after\n"));

        Assert.Single(Events, "pass");
        Assert.DoesNotContain("after", Events);
    }

    [Fact]
    public void ARunFile_ReportsUnderTheCaller_WithBothFilesAndLines()
    {
        var shared = Write("shared", "steps:\n  - t.note: in the file\n  - t.fail: why\n");
        var caller = Write("caller", "steps:\n  - run: shared.ztest.yaml\n  - t.note: after\n");

        Run(caller);

        var step = _steps[1];
        Assert.Equal("caller", step.Scenario);
        Assert.Equal(shared, step.File);
        Assert.Equal(3, step.Line);
        Assert.Equal($"{caller}:2", step.RanFrom);
        Assert.DoesNotContain("after", Events);
        Assert.Contains("caller-failure", string.Join(",", Events));
    }

    [Fact]
    public void ARunFile_SeesOnlyWhatItsWithPassesIn()
    {
        Write("shared", "steps:\n  - t.note: ${given}\n  - t.note: ${outer}\n");
        var caller = Write("caller", "steps:\n  - t.define-text: outer\n  - run: shared.ztest.yaml\n    with: { given: \"${outer}!\" }\n");

        Run(caller);

        Assert.Contains("from a step!", Events);
        Assert.Equal("The name 'outer' isn't defined by an earlier step.", _steps.Last().Detail.Split(": ").Last());
    }

    [Fact]
    public void ARunFile_ThatDefinesAName_LeavesTheCallerWithoutIt()
    {
        Write("shared", "steps:\n  - t.define: lamp\n");
        var caller = Write("caller", "steps:\n  - run: shared.ztest.yaml\n  - run: shared.ztest.yaml\n  - t.use: lamp\n");

        Run(caller);

        Assert.True(_steps[0].Passed);
        Assert.True(_steps[1].Passed);
        Assert.Equal("The name 'lamp' isn't defined by an earlier step.", _steps[2].Detail);
    }

    [Fact]
    public void AWithValue_ThatCantBeWritten_FailsAtTheRun()
    {
        Write("shared", "steps:\n  - t.note: never\n");
        var caller = Write("caller", "steps:\n  - run: shared.ztest.yaml\n    with: { given: \"${nothing}\" }\n");

        Run(caller);

        var step = Assert.Single(_steps);
        Assert.Equal("run", step.Name);
        Assert.Equal(2, step.Line);
        Assert.Contains("The name 'nothing' isn't defined", step.Detail);
    }

    [Fact]
    public void ARunFilesStart_IsNotFollowed()
    {
        Write("shared", "start: main-menu\nsteps:\n  - t.note: x\n");

        Run(Write("caller", "steps:\n  - run: shared.ztest.yaml\n"));

        Assert.Equal(["enter editor", "x"], Events);
    }

    [Fact]
    public void AMissingRunFile_FailsAtLoadWithTheCallersLine()
    {
        Run(Write("caller", "steps:\n  - t.note: x\n  - run: nowhere.ztest.yaml\n"));

        var step = Assert.Single(_steps);
        Assert.Equal("load", step.Name);
        Assert.Equal(3, step.Line);
        Assert.Contains("run: can't find 'nowhere.ztest.yaml' from this file.", step.Detail);
        Assert.Empty(Events);
    }

    [Fact]
    public void ARunCycle_FailsAtLoad()
    {
        Write("b", "steps:\n  - run: a.ztest.yaml\n");

        Run(Write("a", "steps:\n  - run: b.ztest.yaml\n"));

        var step = Assert.Single(_steps);
        Assert.Equal("load", step.Name);
        Assert.Contains("run: 'a.ztest.yaml' is already running here, so it would never end.", step.Detail);
    }

    [Fact]
    public void AFileRunTwice_WithoutACycle_Loads()
    {
        Write("c", "steps:\n  - t.note: c\n");
        Write("b", "steps:\n  - run: c.ztest.yaml\n");

        Run(Write("a", "steps:\n  - run: c.ztest.yaml\n  - run: b.ztest.yaml\n"));

        Assert.Equal(2, Events.Count(item => item == "c"));
    }

    [Fact]
    public void ABrokenFile_FailsBeforeAnyWalk_AndTheOthersStillRun()
    {
        var broken = Write("broken", "steps:\n  - wait: soon\n");
        var fine = Write("fine", "steps:\n  - t.note: fine\n");

        Run(broken, fine);

        Assert.Equal("load", _steps[0].Name);
        Assert.Equal("broken", _steps[0].Scenario);
        Assert.Equal(broken, _steps[0].File);
        Assert.Equal(["enter editor", "fine"], Events);
    }

    [Fact]
    public void AMissingFile_FailsAtLoad()
    {
        Run(Path.Combine(_folder.Path, "nowhere.ztest.yaml"));

        var step = Assert.Single(_steps);
        Assert.Equal("load", step.Name);
        Assert.Equal("nowhere", step.Scenario);
        Assert.False(step.Passed);
    }

    [Fact]
    public void Hooks_RunAroundScenarios_NotBeforeTheFirst_AndAfterAFailedOne()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));

        Run(Write("one", "steps:\n  - t.fail: why\n"), Write("two", "steps:\n  - t.note: x\n"));

        Assert.Equal(
            [
                "run started",
                "enter editor",
                "screenshot one-failure",
                "after one",
                "before two",
                "enter editor",
                "x",
                "after two",
            ],
            Events);
    }

    [Fact]
    public void ABeforeHookThatThrows_FailsThatScenario_ButAfterStillRuns_AndTheRunGoesOn()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder) { ThrowBefore = "two" }));

        Run(Write("one", "steps:\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"), Write("three", "steps:\n  - t.note: c\n"));

        var failed = Assert.Single(_steps, step => !step.Passed);
        Assert.Equal("two", failed.Scenario);
        Assert.Equal("RecordingHooks.BeforeScenario", failed.Name);
        Assert.Equal("InvalidOperationException: hook", failed.Detail);
        Assert.DoesNotContain("b", Events);
        Assert.Contains("after two", Events);
        Assert.Contains("c", Events);
    }

    [Fact]
    public void AHookThatThrowsBeforeItsFirstYield_FailsThatScenario_AndTheRunGoesOn()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new EagerHooks()));

        Run(Write("one", "steps:\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"));

        Assert.Equal(
            ["EagerHooks.AfterScenario", "EagerHooks.BeforeScenario", "EagerHooks.AfterScenario"],
            _steps.Where(step => !step.Passed).Select(step => step.Name));
        Assert.Contains("a", Events);
    }

    [Fact]
    public void ADetail_SetAfterAFailure_DoesntReplaceIt()
    {
        Run(Write("a", "steps:\n  - t.fail-then-detail\n"));

        Assert.Equal("first", Assert.Single(_steps).Detail);
    }

    [Fact]
    public void ScenariosOfOneNameInTwoFolders_StayApart()
    {
        Directory.CreateDirectory(Path.Combine(_folder.Path, "a"));
        Directory.CreateDirectory(Path.Combine(_folder.Path, "b"));

        Run(Write("a/x", "steps:\n  - t.fail: why\n"), Write("b/x", "steps:\n  - t.fail: why\n"));

        Assert.Equal(["a/x", "b/x"], _steps.Select(step => step.Scenario));
        Assert.Contains("screenshot a-x-failure", Events);
        Assert.Contains("screenshot b-x-failure", Events);
    }

    [Fact]
    public void ARunFilePathThePlatformRejects_FailsAtLoad_AndTheOthersStillRun()
    {
        Run(Path.Combine(_folder.Path, "bad\0name.ztest.yaml"), Write("fine", "steps:\n  - t.note: fine\n"));

        Assert.Equal("load", _steps[0].Name);
        Assert.Contains("fine", Events);
    }

    [Fact]
    public void AnAfterHookThatThrows_FailsThatScenario_AndTheRunGoesOn()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder) { ThrowAfter = "one" }));

        Run(Write("one", "steps:\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"));

        Assert.Equal("RecordingHooks.AfterScenario", Assert.Single(_steps, step => !step.Passed).Name);
        Assert.Contains("b", Events);
    }

    [Fact]
    public void ARunStartedHookThatThrows_RunsNoScenarios()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder) { ThrowRunStarted = true }));

        Run(Write("one", "steps:\n  - t.note: a\n"));

        Assert.Equal("RecordingHooks.RunStarted", _steps[0].Name);
        Assert.False(_steps[0].Passed);
        Assert.Equal("not run", _steps[1].Name);
        Assert.Equal("one", _steps[1].Scenario);
        Assert.False(_steps[1].Passed);
        Assert.DoesNotContain("enter editor", Events);
    }

    [Fact]
    public void AStartThatFails_EndsTheRun_NamingWhatDidntRun()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));
        _host.FailEnterAt = 2;

        Run(
            Write("one", "steps:\n  - t.note: a\n"),
            Write("two", "start: main-menu\nsteps:\n  - t.note: b\n"),
            Write("three", "steps:\n  - t.note: c\n"),
            Write("four", "steps:\n  - t.note: d\n"));

        var failed = _steps[1];
        Assert.False(failed.Passed);
        Assert.Equal("start: main-menu", failed.Name);
        Assert.Equal("two", failed.Scenario);
        Assert.Equal(1, failed.Line);
        Assert.Equal("the main menu: not reached after 120 seconds", failed.Detail);

        Assert.Equal(["three", "four"], _steps.Skip(2).Select(step => step.Scenario));
        Assert.All(_steps.Skip(2), step =>
        {
            Assert.False(step.Passed);
            Assert.Equal("not run", step.Name);
            Assert.Equal("two couldn't get to where it starts, so the game is somewhere unknown.", step.Detail);
        });

        Assert.Contains("screenshot two-failure", Events);
        Assert.Contains("after two", Events);
        Assert.DoesNotContain("b", Events);
        Assert.DoesNotContain("before three", Events);
        Assert.DoesNotContain("c", Events);
    }

    [Fact]
    public void AnyExceptionFromTheWalk_CountsAsAStartThatFailed()
    {
        _host.FailEnterAt = 1;
        _host.EnterException = new NullReferenceException("a changed menu");

        Run(Write("one", "steps:\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"));

        Assert.Equal("NullReferenceException: a changed menu", _steps[0].Detail);
        Assert.Equal("not run", _steps[1].Name);
    }

    [Fact]
    public void AStartThatFails_InADefaultStart_HasNoLine()
    {
        _host.FailEnterAt = 1;

        Run(Write("one", "steps:\n  - t.note: a\n"));

        Assert.Equal("start: editor", _steps[0].Name);
        Assert.Equal(0, _steps[0].Line);
    }

    [Fact]
    public void ScenarioState_IsNewForEachScenario_AndSharedWithItsRunFiles()
    {
        Write("shared", "steps:\n  - t.tally\n");

        Run(Write("one", "steps:\n  - t.tally\n  - run: shared.ztest.yaml\n"), Write("two", "steps:\n  - t.tally\n"));

        Assert.Equal(["tally 1", "tally 2", "tally 1"], Events.Where(item => item.StartsWith("tally")));
    }

    [Fact]
    public void AServiceNoPluginGave_FailsTheStepNamingIt()
    {
        Run(Write("one", "steps:\n  - t.wants-a-string\n"));

        Assert.Contains("System.String", Assert.Single(_steps).Detail);
    }

    [Fact]
    public void TwoPluginsGivingOneServiceType_IsAnError()
    {
        _plugins.Add(new TestPluginRegistration("another plugin").Service(new Recorder()));

        var error = RunExpectingError(Write("one", "steps:\n  - t.note: a\n"));

        Assert.Contains("the test plugin", error.Message);
        Assert.Contains("another plugin", error.Message);
    }

    [Fact]
    public void ExceptionsAreListedUnderTheScenario_AndTheStepCanTakeThem()
    {
        _host.WhenEntering = () => _exceptions.Record("Exception: a", "Foo.Bar ()\n");

        Run(Write("one", "steps:\n  - t.takes-exceptions\n"));

        Assert.Equal("one", Assert.Single(_exceptions.Logged).Scenario);
        Assert.Contains("exceptions 1", Events);
        Assert.Contains("scene loads 7", Events);
        Assert.Contains("scenario one", Events);
    }

    [Fact]
    public void AScreenshot_IsNamedAfterItsScenario()
    {
        Run(Write("one", "steps:\n  - t.shot\n"));

        Assert.Contains("screenshot one-view", Events);
    }

    [Fact]
    public void ACoreScenario_Waits_Logs_AndTakesAScreenshot()
    {
        Run(Write("core", "steps:\n  - wait: 2\n  - log: hello\n  - screenshot: view\n"));

        Assert.All(_steps, step => Assert.True(step.Passed, step.Detail));
        Assert.Equal(["wait", "log", "screenshot"], _steps.Select(step => step.Name));
        Assert.Equal("hello", _steps[1].Detail);
        Assert.Contains("screenshot core-view", Events);
    }

    [Fact]
    public void AScreenshotThatFailsAfterAFailedStep_DoesntStopTheRun()
    {
        _host.ScreenshotThrows = true;

        Run(Write("one", "steps:\n  - t.fail: why\n"), Write("two", "steps:\n  - t.note: x\n"));

        Assert.Contains("x", Events);
    }

    private string Write(string name, string yaml)
    {
        var path = Path.Combine(_folder.Path, $"{name}.ztest.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private void Run(params string[] files)
    {
        var error = RunExpectingError(files);
        if (error != null)
            throw new InvalidOperationException("The runner threw", error);
    }

    private Exception RunExpectingError(params string[] files)
    {
        Exception error = null;
        var runner = new ScenarioRunner(_host, _exceptions, StepTypes.Build(_stepTypes), _plugins, _steps.Add, _folder.Path);
        var routine = GuardedRoutine.Run(runner.Run(files), exception => error = exception);
        var frames = 0;
        while (routine.MoveNext())
        {
            if (++frames > 100_000)
                throw new InvalidOperationException("The run never ends");
        }

        return error;
    }

    private sealed class FakeHost : IRunHost
    {
        private readonly Recorder _recorder;
        private float _now;
        private int _enters;

        public FakeHost(Recorder recorder)
        {
            _recorder = recorder;
        }

        public int FailEnterAt { get; set; }

        public Exception EnterException { get; set; }

        public Action WhenEntering { get; set; }

        public bool ScreenshotThrows { get; set; }

        public ManualLogSource Logger { get; } = new ManualLogSource("test");

        // Time goes on every time it's asked, so a wait always ends
        public float Now => _now += 1;

        public IGameEvents Game { get; } = new FakeEvents();

        public LEV_LevelEditorCentral Editor => null;

        public IEnumerator EnterStart(ScenarioStart start, string scenarioFile)
        {
            WhenEntering?.Invoke();
            if (++_enters == FailEnterAt)
                throw EnterException ?? new StartFailedException("the main menu: not reached after 120 seconds");

            _recorder.Events.Add($"enter {start.State.Name}");
            yield return null;
        }

        public IEnumerator Screenshot(string name)
        {
            if (ScreenshotThrows)
                throw new InvalidOperationException("no screen");

            _recorder.Events.Add($"screenshot {name}");
            yield return null;
        }
    }

    private sealed class FakeEvents : IGameEvents
    {
        public int ScenesLoaded => 7;

        public int EditorEntries => 0;

        public int TestModeEntries => 0;

        public int RoundStarts => 0;

        public int LevelLoads => 0;
    }

    /// <summary>
    /// Hooks that aren't iterators, so they throw when they're called.
    /// </summary>
    private sealed class EagerHooks : IScenarioHooks
    {
        public void RunStarted(IRunContext run)
        {
        }

        public IEnumerator BeforeScenario(IScenarioContext scenario)
        {
            throw new InvalidOperationException("hook");
        }

        public IEnumerator AfterScenario(IScenarioContext scenario)
        {
            throw new InvalidOperationException("hook");
        }
    }

    private sealed class RecordingHooks : IScenarioHooks
    {
        private readonly Recorder _recorder;

        public RecordingHooks(Recorder recorder)
        {
            _recorder = recorder;
        }

        public string ThrowBefore { get; init; }

        public string ThrowAfter { get; init; }

        public bool ThrowRunStarted { get; init; }

        public void RunStarted(IRunContext run)
        {
            if (ThrowRunStarted)
                throw new InvalidOperationException("hook");

            run.Get<Recorder>().Events.Add("run started");
        }

        public IEnumerator BeforeScenario(IScenarioContext scenario)
        {
            if (ThrowBefore == scenario.ScenarioName)
                throw new InvalidOperationException("hook");

            _recorder.Events.Add($"before {scenario.ScenarioName}");
            yield break;
        }

        public IEnumerator AfterScenario(IScenarioContext scenario)
        {
            if (ThrowAfter == scenario.ScenarioName)
                throw new InvalidOperationException("hook");

            _recorder.Events.Add($"after {scenario.ScenarioName}");
            yield break;
        }
    }
}
