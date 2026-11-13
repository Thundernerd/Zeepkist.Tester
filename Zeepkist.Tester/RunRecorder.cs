using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Runs what a run's units do guarded, and records it: each result reported, what went wrong logged, and a screenshot where
/// a unit failed.
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
    /// Runs <paramref name="routine"/> for <paramref name="context"/>'s step, failing the step with what it throws, then gives
    /// <paramref name="recorded"/> the step's result, for the caller to say which step it was. An exception
    /// <paramref name="stops"/> holds for only stops the routine, which fails nothing.
    /// </summary>
    public IEnumerator RunStep(StepContext context, IEnumerator routine, Action<RunStep> recorded, Func<Exception, bool> stops = null)
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
