using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Steps;
using UnityEngine;
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
        typeof(ExpectBlankScreenshot),
        typeof(ExpectNoExceptions),
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
        Assert.Equal("run.json names no scenarios or tests, so this run tested nothing.", step.Detail);
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
    public void AWithThatDoesntPassWhatTheFileTakes_FailsAtLoadWithTheCallersLine()
    {
        Write("shared", "with: [given]\nsteps:\n  - t.note: ${given}\n");

        Run(Write("caller", "steps:\n  - t.note: x\n  - run: shared.ztest.yaml\n"));

        var step = Assert.Single(_steps);
        Assert.Equal("load", step.Name);
        Assert.Equal(3, step.Line);
        Assert.Contains("run: 'shared.ztest.yaml' takes 'given', but with: doesn't pass it.", step.Detail);
        Assert.Empty(Events);
    }

    [Fact]
    public void AFileWithAWithHeader_RunsWithWhatItsCallerPasses()
    {
        Write("shared", "with: [given]\nsteps:\n  - t.note: ${given}\n");

        Run(Write("caller", "steps:\n  - run: shared.ztest.yaml\n    with: { given: hi }\n"));

        Assert.Equal(["enter editor", "hi"], Events);
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

    [Fact]
    public void TheMouseIsGivenBack_AfterEachScenario_AfterItsAfterHooks()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));
        _host.Input.RecordsGiveBack = true;

        Run(Write("one", "steps:\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"));

        Assert.Equal(
            ["run started", "enter editor", "a", "after one", "give back", "before two", "enter editor", "b", "after two", "give back"],
            Events);
    }

    [Fact]
    public void TheMouseIsGivenBack_AfterAFailedScenario_EvenWhenAnAfterHookThrows()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder) { ThrowAfter = "one" }));
        _host.Input.RecordsGiveBack = true;

        Run(Write("one", "steps:\n  - t.fail: why\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"));

        Assert.Equal(
            ["run started", "enter editor", "screenshot one-failure", "give back", "before two", "enter editor", "b", "after two", "give back"],
            Events);
    }

    [Fact]
    public void TheMouseIsGivenBack_AfterAFailedStart_BeforeTheRunEnds()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));
        _host.Input.RecordsGiveBack = true;
        _host.FailEnterAt = 1;

        Run(Write("one", "steps:\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"));

        Assert.Equal(["run started", "screenshot one-failure", "after one", "give back"], Events);
    }

    [Fact]
    public void TheMouseIsGivenBack_AfterABeforeHookFails()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder) { ThrowBefore = "two" }));
        _host.Input.RecordsGiveBack = true;

        Run(Write("one", "steps:\n  - t.note: a\n"), Write("two", "steps:\n  - t.note: b\n"));

        Assert.Equal(["run started", "enter editor", "a", "after one", "give back", "after two", "give back"], Events);
    }

    [Fact]
    public void AScenarioStartsOnTheRouteOfItsHeader_WhateverAStepSwitchedTheOneBeforeTo()
    {
        Run(
            Write("one", "steps:\n  - t.route\n  - t.switch-to-os\n  - t.route\n"),
            Write("two", "steps:\n  - t.route\n"),
            Write("three", "input: os\nsteps:\n  - t.route\n"),
            Write("four", "input: in-process\nsteps:\n  - t.route\n"));

        Assert.Equal(["route InProcess", "route Os", "route InProcess", "route Os", "route InProcess"], Events.Where(item => item.StartsWith("route", StringComparison.Ordinal)));
    }

    [Fact]
    public void Screenshot_OfABlankScreen_FailsTheStep_SayingWhichColor()
    {
        _host.BlankColor = "#00000000";
        var file = Write("blank", "steps:\n  - screenshot: shot\n");

        Run(file);

        var step = Assert.Single(_steps);
        Assert.False(step.Passed);
        Assert.Equal("The screenshot is blank: every pixel is #00000000.", step.Detail);
        Assert.Equal("more than one color", step.Expected);
        Assert.Equal("every pixel #00000000", step.Actual);
    }

    [Fact]
    public void ExpectBlankScreenshot_CoversTheFrame_AndPassesOnWhatItFinds()
    {
        _host.BlankColor = "#FF00FFFF";
        var file = Write("covered", "steps:\n  - expect-blank-screenshot: shot\n");

        Run(file);

        var step = Assert.Single(_steps);
        Assert.True(step.Passed);
        Assert.Equal("Every pixel is #FF00FFFF.", step.Detail);
        Assert.Contains("covered screenshot covered-shot", Events);
    }

    [Fact]
    public void ExpectBlankScreenshot_OfAFrameThatStillShowsSomething_FailsTheStep()
    {
        var file = Write("covered", "steps:\n  - expect-blank-screenshot: shot\n");

        Run(file);

        var step = Assert.Single(_steps);
        Assert.False(step.Passed);
        Assert.Equal("The screen was covered in one color, but the screenshot isn't blank.", step.Detail);
    }

    [Fact]
    public void ExpectNoExceptions_WithNoneLogged_Passes()
    {
        var file = Write("clean", "steps:\n  - expect-no-exceptions\n");

        Run(file);

        var step = Assert.Single(_steps);
        Assert.True(step.Passed);
        Assert.Equal("0", step.Actual);
    }

    [Fact]
    public void ExpectNoExceptions_ListsWhatWasLogged_AndFailsTheStep()
    {
        _host.WhenEntering = () =>
        {
            _exceptions.Record("Exception: boom", "at Here\nat There");
            _exceptions.Record("Exception: boom", "at Here\nat There");
            _exceptions.Record("Exception: bang", string.Empty);
        };
        var file = Write("thrown", "steps:\n  - expect-no-exceptions\n");

        Run(file);

        var step = Assert.Single(_steps);
        Assert.False(step.Passed);
        Assert.Equal("3 exceptions logged", step.Detail);
        Assert.Equal("0", step.Expected);
        Assert.Equal("3: 2× Exception: boom at at Here; Exception: bang", step.Actual);
    }

    [Fact]
    public void ATest_RunsFromItsStart_AfterTheScenarios_EachStepAsItsOwnEntry()
    {
        Run([Write("a", "steps:\n  - t.note: scenario\n")], ["RunnerTests.TwoNotes", "RunnerTests.FromTheMenu"]);

        Assert.Equal(["enter editor", "scenario", "enter editor", "a", "b", "enter main-menu", "menu"], Events);
        Assert.Equal(
            ["a t.note", "RunnerTests.TwoNotes t.note", "RunnerTests.TwoNotes t.note", "RunnerTests.FromTheMenu t.note"],
            _steps.Select(step => $"{step.Scenario} {step.Name}"));
        Assert.All(_steps, step => Assert.True(step.Passed));
        Assert.Equal(string.Empty, _steps[1].File);
        Assert.Equal(0, _steps[1].Line);
    }

    [Fact]
    public void AFailedStep_StopsItsTestWithAScreenshot_AndTheRunGoesOn()
    {
        Run([], ["RunnerTests.FailsHalfway", "RunnerTests.TwoNotes"]);

        Assert.Equal(["enter editor", "before", "screenshot RunnerTests-FailsHalfway-failure", "enter editor", "a", "b"], Events);
        Assert.Equal(["t.note pass", "t.fail FAIL", "t.note pass", "t.note pass"], _steps.Select(step => $"{step.Name} {(step.Passed ? "pass" : "FAIL")}"));
        Assert.Equal("why", _steps[1].Detail);
        Assert.Equal("expected", _steps[1].Expected);
    }

    [Theory]
    [InlineData("RunnerTests.Throws", "InvalidOperationException: boom")]
    [InlineData("RunnerTests.FailsItself", "not right")]
    [InlineData("RunnerTests.StopsItself", "stopped")]
    public void ATestThatFailsOutsideAStep_IsAFailedTestEntry(string test, string detail)
    {
        Run([], [test]);

        var failed = _steps.Last();
        Assert.Equal("test", failed.Name);
        Assert.Equal(test, failed.Scenario);
        Assert.False(failed.Passed);
        Assert.Equal(detail, failed.Detail);
        Assert.Contains($"screenshot {test.Replace('.', '-')}-failure", Events);
        Assert.DoesNotContain("after", Events);
    }

    [Fact]
    public void ATestThatChecksItself_OrRunsNoStep_HasAnEntryOfItsOwn()
    {
        Run([], ["RunnerTests.ChecksItself", "RunnerTests.Empty"]);

        Assert.Equal(["t.note RunnerTests.ChecksItself pass ", "test RunnerTests.ChecksItself pass 3", "test RunnerTests.Empty pass "], _steps.Select(step => $"{step.Name} {step.Scenario} {(step.Passed ? "pass" : "FAIL")} {step.Actual}"));
    }

    [Fact]
    public void AClassName_SelectsEachOfItsTests_OnceEach_AndAnUnknownNameFailsAtLoad()
    {
        Run([], ["RunnerTests.Empty", "RunnerTests", "Nothing.Here"]);

        Assert.Equal("load Nothing.Here FAIL", $"{_steps[0].Name} {_steps[0].Scenario} {(_steps[0].Passed ? "pass" : "FAIL")}");
        Assert.Equal("No [InGameTest] is named Nothing.Here: a test is selected by its class's name, or by Class.Method.", _steps[0].Detail);
        var tests = _steps.Skip(1).Select(step => step.Scenario).Distinct().ToList();
        Assert.Equal("RunnerTests.Empty", tests[0]);
        Assert.Equal(1, tests.Count(test => test == "RunnerTests.Empty"));
        Assert.Contains("RunnerTests.TwoNotes", tests);
        Assert.Contains("RunnerTests.Throws", tests);
    }

    [Fact]
    public void ATest_SharesItsNamedValuesWithItsSteps_AndLeavesTheirTextAsItIs()
    {
        Run([], ["RunnerTests.NamedValues"]);

        Assert.Equal(["t.define-text", "t.use-text", "t.raw", "test"], _steps.Select(step => step.Name));
        Assert.All(_steps, step => Assert.True(step.Passed, step.Detail));
        Assert.Equal("${lamp} $${", _steps[2].Detail);
        Assert.Equal("from a step", _steps[3].Detail);
    }

    [Fact]
    public void ATestsStep_IsCheckedForItsStatesFirst()
    {
        Run([], ["RunnerTests.NeedsAState"]);

        var step = Assert.Single(_steps);
        Assert.Equal("t.needs-a-state", step.Name);
        Assert.False(step.Passed);
        Assert.Equal("t.needs-a-state needs t.never: Add t.note first.", step.Detail);
        Assert.DoesNotContain("ran without its state", Events);
    }

    [Fact]
    public void AScenariosStep_CantRunAStep()
    {
        Run(Write("a", "steps:\n  - t.runs-a-step\n"));

        var step = Assert.Single(_steps);
        Assert.False(step.Passed);
        Assert.Equal("A scenario's step can't run Note: context.Run is for [InGameTest] methods.", step.Detail);
    }

    [Fact]
    public void Hooks_RunAroundTests_AsAroundScenarios()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));

        Run([Write("one", "steps:\n  - t.note: x\n")], ["RunnerTests.Empty"]);

        Assert.Equal(["run started", "enter editor", "x", "after one", "before RunnerTests.Empty", "enter editor", "after RunnerTests.Empty"], Events);
    }

    [Fact]
    public void TheMouseIsGivenBack_AfterEachTest_AfterItsAfterHooks_AlsoAfterAFailedOne()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));
        _host.Input.RecordsGiveBack = true;

        Run([], ["RunnerTests.FailsHalfway", "RunnerTests.TwoNotes"]);

        Assert.Equal(
            [
                "run started",
                "enter editor",
                "before",
                "screenshot RunnerTests-FailsHalfway-failure",
                "after RunnerTests.FailsHalfway",
                "give back",
                "before RunnerTests.TwoNotes",
                "enter editor",
                "a",
                "b",
                "after RunnerTests.TwoNotes",
                "give back",
            ],
            Events);
    }

    [Fact]
    public void TheMouseIsGivenBack_AfterATestsStartFails()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));
        _host.Input.RecordsGiveBack = true;
        _host.FailEnterAt = 1;

        Run([], ["RunnerTests.Empty"]);

        Assert.Equal(["run started", "screenshot RunnerTests-Empty-failure", "after RunnerTests.Empty", "give back"], Events);
    }

    [Fact]
    public void AStartThatFails_ListsTheTestsLeftAsNotRun()
    {
        _host.FailEnterAt = 1;

        Run([Write("one", "steps:\n  - t.note: x\n")], ["RunnerTests.Empty"]);

        Assert.Equal(["one start: editor", "RunnerTests.Empty not run"], _steps.Select(step => $"{step.Scenario} {step.Name}"));
    }

    [Fact]
    public void OnlyTests_StillRunTheRunStartedHooks()
    {
        _plugins.Add(new TestPluginRegistration("hooks").Hooks(new RecordingHooks(_recorder)));

        Run([], ["RunnerTests.Empty"]);

        Assert.Equal(["run started", "enter editor", "after RunnerTests.Empty"], Events);
    }

    [Fact]
    public void ATestWhoseClassCantBeMade_FailsThatTest_AndTheRunGoesOn()
    {
        Run([], ["BrokenRunnerTests.CantBeMade", "RunnerTests.TwoNotes"]);

        Assert.Equal("test BrokenRunnerTests.CantBeMade FAIL InvalidOperationException: no instance", $"{_steps[0].Name} {_steps[0].Scenario} {(_steps[0].Passed ? "pass" : "FAIL")} {_steps[0].Detail}");
        Assert.Contains("b", Events);
    }

    [Fact]
    public void ATestsOwnFailure_BeforeAFailedStep_IsStillRecorded()
    {
        Run([], ["RunnerTests.FailsItselfThenAStep"]);

        Assert.Equal(["t.fail FAIL why", "test FAIL first its own"], _steps.Select(step => $"{step.Name} {(step.Passed ? "pass" : "FAIL")} {step.Detail}"));
        Assert.Single(Events, @event => @event.StartsWith("screenshot", StringComparison.Ordinal));
    }

    private string Write(string name, string yaml)
    {
        var path = Path.Combine(_folder.Path, $"{name}.ztest.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private void Run(params string[] files)
    {
        Run(files, []);
    }

    private void Run(string[] files, string[] tests)
    {
        var error = RunExpectingError(files, tests);
        if (error != null)
            throw new InvalidOperationException("The runner threw", error);
    }

    private Exception RunExpectingError(params string[] files)
    {
        return RunExpectingError(files, []);
    }

    private Exception RunExpectingError(string[] files, string[] tests)
    {
        Exception error = null;
        var runner = new ScenarioRunner(_host, _exceptions, new RunCatalog(StepTypes.Build(_stepTypes), InGameTests.Build(new[] { typeof(RunnerTests), typeof(BrokenRunnerTests) }.SelectMany(type => type.GetMethods()).Where(method => method.IsDefined(typeof(InGameTestAttribute), false)))), _plugins, _steps.Add, _folder.Path);
        var routine = GuardedRoutine.Run(runner.Run(files, tests), exception => error = exception);
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
            Input = new RecordingInput(recorder);
        }

        public int FailEnterAt { get; set; }

        public Exception EnterException { get; set; }

        public Action WhenEntering { get; set; }

        public bool ScreenshotThrows { get; set; }

        /// <summary>
        /// What each screenshot comes out as, null for one with something on it.
        /// </summary>
        public string BlankColor { get; set; }

        public string LastBlankColor { get; private set; }

        public ManualLogSource Logger { get; } = new ManualLogSource("test");

        // Time goes on every time it's asked, so a wait always ends
        public float Now => _now += 1;

        public RecordingInput Input { get; }

        IInput IRunHost.Input => Input;

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

        public IEnumerator Screenshot(string name, bool covered = false)
        {
            if (ScreenshotThrows)
                throw new InvalidOperationException("no screen");

            _recorder.Events.Add(covered ? $"covered screenshot {name}" : $"screenshot {name}");
            LastBlankColor = BlankColor;
            yield return null;
        }
    }

    /// <summary>
    /// The harness's input with no drivers, which writes down each time it's given back when asked to.
    /// </summary>
    private sealed class RecordingInput : IInput
    {
        private readonly IInput _input = new TesterInput(new InProcessDrivers(), new NoActions(), () => 0);
        private readonly Recorder _recorder;

        public RecordingInput(Recorder recorder)
        {
            _recorder = recorder;
        }

        public bool RecordsGiveBack { get; set; }

        public InputRoute Route
        {
            get => _input.Route;
            set => _input.Route = value;
        }

        public IEnumerator Press(Keys keys) => _input.Press(keys);

        public IEnumerator Type(string text) => _input.Type(text);

        public IEnumerator Hold(Keys keys, Seconds duration) => _input.Hold(keys, duration);

        public IEnumerator PressAction(string action) => _input.PressAction(action);

        public IEnumerator HoldAction(string action, Seconds duration, float value = 1) => _input.HoldAction(action, duration, value);

        public IEnumerator MoveMouse(Vector2 screenPoint) => _input.MoveMouse(screenPoint);

        public IEnumerator Click(Vector2 screenPoint, MouseButton button = MouseButton.Left) => _input.Click(screenPoint, button);

        public IEnumerator Drag(Vector2 from, IReadOnlyList<DragMove> moves, MouseButton button = MouseButton.Left) => _input.Drag(from, moves, button);

        public void GiveBack()
        {
            if (RecordsGiveBack)
                _recorder.Events.Add("give back");

            _input.GiveBack();
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

    private sealed class NoActions : IActionDriver
    {
        public bool Set(string action, float value)
        {
            return false;
        }

        public void GiveBack()
        {
        }
    }
}
