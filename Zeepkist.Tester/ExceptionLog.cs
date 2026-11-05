using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The exceptions the game logged while the run went on, each scenario's under its name, for the results and for
/// expect-no-exceptions.
/// </summary>
internal sealed class ExceptionLog
{
    // Those logged since the last expect-no-exceptions, or since the scenario started
    private readonly List<LoggedException> _sinceCheck = [];

    private string _scenario = string.Empty;

    public List<LoggedException> Logged { get; } = [];

    /// <summary>
    /// Lists what's logged from now on under <paramref name="scenario"/>, and starts what the next check looks at.
    /// </summary>
    public void StartScenario(string scenario)
    {
        _scenario = scenario;
        _sinceCheck.Clear();
    }

    public void Record(string message, string stackTrace)
    {
        var frame = FirstFrame(stackTrace);
        Count(Logged, message, frame);
        Count(_sinceCheck, message, frame);
    }

    /// <summary>
    /// What was logged since the last call, or since the scenario started.
    /// </summary>
    public List<LoggedException> TakeSinceCheck()
    {
        List<LoggedException> taken = [.. _sinceCheck];
        _sinceCheck.Clear();
        return taken;
    }

    private static string FirstFrame(string stackTrace)
    {
        foreach (var line in stackTrace.Split('\n'))
        {
            if (line.Trim().Length > 0)
                return line.Trim();
        }

        return string.Empty;
    }

    private void Count(List<LoggedException> list, string message, string frame)
    {
        var logged = list.Find(e => e.Scenario == _scenario && e.Message == message && e.Frame == frame);
        if (logged != null)
            logged.Count++;
        else
            list.Add(new LoggedException { Scenario = _scenario, Message = message, Frame = frame });
    }
}
