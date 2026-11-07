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
    private readonly IRunHost _host;
    private string _detail = string.Empty;

    public StepContext(ScenarioContext scenario, IRunHost host, IExceptionLog exceptions, IValues values, Dictionary<Type, object> state)
        : base(scenario, scenario.ScenarioName, state)
    {
        _host = host;
        Exceptions = exceptions;
        Values = values;
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
            Fail("This step needs the level editor, and the game isn't in it.");

        return editor;
    }

    private void Record(string detail, string expected, string actual)
    {
        _detail = detail ?? string.Empty;
        Expected = expected;
        Actual = actual;
    }
}
