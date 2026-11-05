namespace TNRD.Zeepkist.Tester;

/// <summary>
/// One step's result in results.json. The walk to the level editor has no <see cref="Scenario"/> or <see cref="Line"/>.
/// </summary>
internal sealed class RunStep
{
    public string Name { get; set; }
    public bool Passed { get; set; }
    public string Detail { get; set; } = "";
    public string Scenario { get; set; } = "";
    public int Line { get; set; }

    /// <summary>
    /// Set by the checks, null for other steps.
    /// </summary>
    public string Expected { get; set; }

    /// <summary>
    /// Set by the checks, null for other steps.
    /// </summary>
    public string Actual { get; set; }

    public double Seconds { get; set; }

    public static RunStep Pass(string name)
    {
        return new RunStep { Name = name, Passed = true };
    }

    public static RunStep Fail(string name, string detail)
    {
        return new RunStep { Name = name, Passed = false, Detail = detail };
    }
}
