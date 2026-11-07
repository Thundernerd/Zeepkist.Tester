using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The exceptions the game logged during the running scenario.
/// </summary>
public interface IExceptionLog
{
    /// <summary>
    /// What was logged since the last call, or since the scenario started.
    /// </summary>
    public IReadOnlyList<LoggedException> TakeSinceCheck();
}
