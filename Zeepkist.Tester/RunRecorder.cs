using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Records what a run's units do. It reports each result, logs what went wrong, and takes a screenshot where a unit failed.
/// </summary>
internal sealed class RunRecorder
{
    private readonly IRunHost _host;
    private readonly Action<RunStep> _report;

    /// <param name="host">The game.</param>
    /// <param name="report">Takes each step's result.</param>
    public RunRecorder(IRunHost host, Action<RunStep> report)
    {
        _host = host;
        _report = report;
    }

    public static string Describe(Exception exception)
    {
        return exception is StepFailedException ? exception.Message : $"{exception.GetType().Name}: {exception.Message}";
    }

    /// <summary>
    /// A hook or a test's class needn't be an iterator, so the call itself can throw too.
    /// </summary>
    public static IEnumerator Called(Func<IEnumerator> routine)
    {
        yield return routine();
    }

    public void Report(RunStep step)
    {
        _report(step);
    }

    /// <summary>
    /// Runs a step's routine and fails the step with what it throws. Then fills in what the step found, which the caller reports.
    /// </summary>
    /// <param name="context">The step's context.</param>
    /// <param name="routine">What the step does.</param>
    /// <param name="recorded">The step's result, which says which step it was.</param>
    /// <param name="stops">Says whether an exception only stops the routine. Such an exception fails nothing.</param>
    public IEnumerator RunGuarded(StepContext context, IEnumerator routine, RunStep recorded, Func<Exception, bool> stops = null)
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

        recorded.Passed = !context.Failed;
        recorded.Detail = context.Detail;
        recorded.Expected = context.Expected;
        recorded.Actual = context.Actual;
        recorded.Seconds = Seconds(startedAt);
    }

    /// <summary>
    /// Not being able to take one shouldn't change what the run found.
    /// </summary>
    public IEnumerator FailureScreenshot(RunUnit unit)
    {
        yield return GuardedRoutine.Run(_host.Screenshot(ScreenshotNames.For(unit.Name, "failure")), LogError);
    }

    public void LogError(Exception exception)
    {
        _host.Logger.LogError($"{TestRun.Marker} {exception}");
    }

    public double Seconds(float startedAt)
    {
        return Math.Round(_host.Now - startedAt, 2);
    }
}
