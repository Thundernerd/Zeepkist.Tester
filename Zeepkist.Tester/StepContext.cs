using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What one step runs against, and what came of it.
/// </summary>
internal sealed class StepContext : ScenarioContext, IStepContext
{
    public const string NotInEditor = "This step needs the level editor, and the game isn't in it.";

    private readonly IRunHost _host;
    private readonly Func<Step, IEnumerator> _run;
    private string _detail = string.Empty;

    /// <param name="scenario">The scenario or C# test it's part of.</param>
    /// <param name="host">The game.</param>
    /// <param name="exceptions">What the game logged.</param>
    /// <param name="values">The named values of the running scope.</param>
    /// <param name="state">What <see cref="ScenarioContext.Scenario{T}"/> keeps, which the scenario's steps share.</param>
    /// <param name="run">What <see cref="Run"/> does, or null for a scenario's step, which can't run steps.</param>
    public StepContext(ScenarioContext scenario, IRunHost host, IExceptionLog exceptions, IValues values, Dictionary<Type, object> state, Func<Step, IEnumerator> run)
        : base(scenario, scenario.ScenarioName, state)
    {
        _host = host;
        _run = run;
        Exceptions = exceptions;
        Values = values;
        Blocks = new EditorBlocks(this, () => _host.Editor);
    }

    public string Detail
    {
        get => _detail;
        set
        {
            if (!Failed)
                _detail = value;
        }
    }

    public string Expected { get; private set; }

    public string Actual { get; private set; }

    public bool Failed { get; private set; }

    public IValues Values { get; }

    public IInput Input => _host.Input;

    public IGameEvents Game => _host.Game;

    public IExceptionLog Exceptions { get; }

    public IEditorBlocks Blocks { get; }

    public string LastScreenshotBlankColor => _host.LastBlankColor;

    public void Pass(string detail, string expected, string actual)
    {
        if (!Failed)
            Record(detail, expected, actual);
    }

    public void Fail(string detail, string expected = null, string actual = null)
    {
        if (Failed)
            return;

        Failed = true;
        Record(detail, expected, actual);
    }

    public IEnumerator WaitSeconds(Seconds seconds)
    {
        var until = _host.Now + seconds.Value;
        while (_host.Now < until)
            yield return null;
    }

    public IEnumerator WaitFrames(int frames)
    {
        for (var i = 0; i < frames; i++)
            yield return null;
    }

    public IEnumerator WaitUntil(Func<bool> condition, Seconds timeout, string what)
    {
        var giveUpAt = _host.Now + timeout.Value;
        while (!condition())
        {
            if (_host.Now > giveUpAt)
                throw new StepFailedException($"Waited {timeout.Value.ToString(CultureInfo.InvariantCulture)} seconds for {what}.");

            yield return null;
        }
    }

    public IEnumerator Screenshot(FileName name, bool covered = false)
    {
        return _host.Screenshot(ScreenshotNames.For(ScenarioName, name.Value), covered);
    }

    public LEV_LevelEditorCentral RequireEditor()
    {
        var editor = _host.Editor;
        if (editor is null)
            Fail(NotInEditor);

        return editor;
    }

    public IEnumerator Run(Step step)
    {
        if (_run != null)
            return _run(step);

        throw new StepFailedException($"A scenario's step can't run {step?.GetType().Name ?? "a step"}: context.Run is for [InGameTest] methods.");
    }

    private void Record(string detail, string expected, string actual)
    {
        _detail = detail ?? string.Empty;
        Expected = expected;
        Actual = actual;
    }
}
