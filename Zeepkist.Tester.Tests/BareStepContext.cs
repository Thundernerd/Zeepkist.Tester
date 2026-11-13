using System;
using System.Collections;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// A step context with only its named values, for tests of what doesn't need the rest.
/// </summary>
public abstract class BareStepContext : IStepContext
{
    public IValues Values { get; } = new ScenarioValues();

    public ManualLogSource Logger => throw new NotSupportedException();

    public string ScenarioName => throw new NotSupportedException();

    public string Detail { get; set; }

    public bool Failed => throw new NotSupportedException();

    public IInput Input => throw new NotSupportedException();

    public IGameEvents Game => throw new NotSupportedException();

    public IExceptionLog Exceptions => throw new NotSupportedException();

    public string LastScreenshotBlankColor => throw new NotSupportedException();

    public T Get<T>()
        where T : class
    {
        throw new NotSupportedException();
    }

    public T Scenario<T>()
        where T : class, new()
    {
        throw new NotSupportedException();
    }

    public virtual void Pass(string detail, string expected, string actual)
    {
        throw new NotSupportedException();
    }

    public virtual void Fail(string detail, string expected = null, string actual = null)
    {
        throw new NotSupportedException();
    }

    public IEnumerator WaitSeconds(Seconds seconds)
    {
        throw new NotSupportedException();
    }

    public IEnumerator WaitFrames(int frames)
    {
        throw new NotSupportedException();
    }

    public IEnumerator WaitUntil(Func<bool> condition, Seconds timeout, string what)
    {
        throw new NotSupportedException();
    }

    public IEnumerator Screenshot(FileName name, bool covered = false)
    {
        throw new NotSupportedException();
    }

    public LEV_LevelEditorCentral RequireEditor()
    {
        throw new NotSupportedException();
    }

    public IEnumerator Run(Step step)
    {
        throw new NotSupportedException();
    }
}
