using System;
using System.Collections;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// What a step runs against.
/// </summary>
public interface IStepContext : IScenarioContext
{
    /// <summary>
    /// What the results say about the step.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Whether <see cref="Fail"/> was called. A failure can't be taken back, so the first one is what the results show.
    /// </summary>
    public bool Failed { get; }

    /// <summary>
    /// The named values of the running scope.
    /// </summary>
    public IValues Values { get; }

    public IGameEvents Game { get; }

    public IExceptionLog Exceptions { get; }

    /// <summary>
    /// Records a check that held. This does nothing after <see cref="Fail"/>.
    /// </summary>
    public void Pass(string detail, string expected, string actual);

    /// <summary>
    /// Fails the step, and the rest of <see cref="Step.Run"/> still runs. To stop it, <c>yield break</c> or throw a <see cref="StepFailedException"/>.
    /// </summary>
    public void Fail(string detail, string expected = null, string actual = null);

    public IEnumerator WaitSeconds(Seconds seconds);

    public IEnumerator WaitFrames(int frames);

    /// <summary>
    /// Waits a frame at a time until <paramref name="condition"/> holds. Giving up stops the step and fails it, with a
    /// <see cref="StepFailedException"/> saying <paramref name="what"/> it waited for.
    /// </summary>
    public IEnumerator WaitUntil(Func<bool> condition, Seconds timeout, string what);

    /// <summary>
    /// Saves a screenshot in the run's results, named after the scenario and <paramref name="name"/>.
    /// </summary>
    public IEnumerator Screenshot(FileName name);

    /// <summary>
    /// The level editor. Outside it this fails the step and gives null.
    /// </summary>
    public LEV_LevelEditorCentral RequireEditor();
}
