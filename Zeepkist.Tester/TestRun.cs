using System;
using System.Collections;
using BepInEx.Logging;
using UnityEngine;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Checks the run has the right builds, records what the game runs at, and quits. An exception anywhere fails the run,
/// and still quits the game, instead of leaving the runner to time out.
/// </summary>
internal sealed class TestRun : IDisposable
{
    // Starts every line this plugin logs, so a run's lines can be picked out of the BepInEx log
    public const string Marker = "[ZTEST]";

    private readonly ManualLogSource _logger;
    private readonly RunResults _results;
    private readonly ExceptionLog _exceptions;
    private readonly RunRequest _request;
    private readonly string _folder;
    private readonly GameEvents _game = new GameEvents();
    private readonly Screenshots _screenshots;

    public TestRun(ManualLogSource logger, RunResults results, ExceptionLog exceptions, RunRequest request, string folder)
    {
        _logger = logger;
        _results = results;
        _exceptions = exceptions;
        _request = request;
        _folder = folder;
        _screenshots = new Screenshots(results, logger);
        Application.logMessageReceived += OnLogMessage;
    }

    public void Dispose()
    {
        Application.logMessageReceived -= OnLogMessage;
        _game.Dispose();
    }

    public IEnumerator Run()
    {
        Exception error = null;
        yield return GuardedRoutine.Run(Checks(), exception => error = exception);
        if (error != null)
        {
            _logger.LogError(error);
            Step(RunStep.Fail("no errors", $"{error.GetType().Name}: {error.Message}"));
        }

        if (error != null || _results.AnyStepFailed)
            yield return GuardedRoutine.Run(_screenshots.Take("failure"), _logger.LogError);

        yield return Finish();
    }

    private IEnumerator Checks()
    {
        // BepInEx flushes its log file every couple of seconds, and the copies it skipped are logged before plugins load
        yield return new WaitForSecondsRealtime(3);
        _results.Display = Display();
        _logger.LogInfo($"{Marker} display {_results.Display}");
        foreach (var step in BuildCheck.Run(_request, _folder))
            Step(step);

        if (!_results.AnyStepFailed)
            yield return _screenshots.Take("start");
    }

    /// <summary>
    /// The screen is the monitor the game's window is on, or the desktop's size without one.
    /// </summary>
    private static string Display()
    {
        var screen = Screen.currentResolution;
        return $"{Screen.width}×{Screen.height} px, {Screen.fullScreenMode}, screen {screen.width}×{screen.height} px";
    }

    private void Step(RunStep step)
    {
        _results.Add(step);
        if (step.Passed)
            _logger.LogInfo($"{Marker} {step}");
        else
            _logger.LogError($"{Marker} {step}");
    }

    private void OnLogMessage(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Exception)
            _exceptions.Record(message, stackTrace);
    }

    private IEnumerator Finish()
    {
        // What's logged from here on is the game quitting, which the runner reads from the log once it has quit
        Application.logMessageReceived -= OnLogMessage;
        _results.Finish();
        _logger.LogInfo($"{Marker} run {_results.RunId} {(_results.Passed ? "passed" : "failed")}");
        FlushDiskLog();

        // Quitting this way skips the editor's unsaved-level prompt
        yield return null;
        Application.Quit();
    }

    /// <summary>
    /// BepInEx flushes its log file every couple of seconds, and the game can quit before the next time, so the runner
    /// would read a log that stops before the run's end.
    /// </summary>
    private static void FlushDiskLog()
    {
        foreach (var listener in BepInEx.Logging.Logger.Listeners)
        {
            if (listener is DiskLogListener disk)
                disk.LogWriter?.Flush();
        }
    }
}
