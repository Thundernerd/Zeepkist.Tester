namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// One exception the game logged during a scenario in results.json, counted each time it's logged again from the same
/// place. The walk to the level editor has no <see cref="Scenario"/>.
/// </summary>
public sealed class LoggedException
{
    public string Scenario { get; set; } = "";

    /// <summary>
    /// The exception's type and message, as Unity logs it.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// The first line of its stack trace, empty when it has none.
    /// </summary>
    public string Frame { get; set; } = "";

    public int Count { get; set; } = 1;

    public override string ToString()
    {
        var count = Count > 1 ? $"{Count}× " : string.Empty;
        var frame = Frame.Length > 0 ? $" at {Frame}" : string.Empty;
        return count + Message + frame;
    }
}
