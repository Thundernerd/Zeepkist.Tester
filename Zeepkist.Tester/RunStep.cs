namespace TNRD.Zeepkist.Tester;

/// <summary>
/// One step's result in results.json. What a run checks outside a scenario, and the walks to where each one starts, have
/// no <see cref="Scenario"/> or <see cref="Line"/>.
/// </summary>
internal sealed class RunStep
{
    public string Name { get; set; }
    public bool Passed { get; set; }
    public string Detail { get; set; } = "";
    public string Scenario { get; set; } = "";

    /// <summary>
    /// The file the step is written in, which is another than the scenario's inside a <c>run:</c>.
    /// </summary>
    public string File { get; set; } = "";

    /// <summary>
    /// The line in <see cref="File"/>.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The <c>run:</c> nodes that led to <see cref="File"/>, as <c>file:line</c>, outermost first. Empty outside a <c>run:</c>.
    /// </summary>
    public string RanFrom { get; set; } = "";

    /// <summary>
    /// Which pass of each <c>for</c> and <c>repeat</c> around the step, such as <c>h=64, repeat 2/3</c>. Empty outside them.
    /// </summary>
    public string Passes { get; set; } = "";

    /// <summary>
    /// Set by the checks, null for other steps.
    /// </summary>
    public string Expected { get; set; }

    /// <summary>
    /// Set by the checks, null for other steps.
    /// </summary>
    public string Actual { get; set; }

    public double Seconds { get; set; }

    public override string ToString()
    {
        var scenario = Scenario.Length > 0 ? $"{Scenario}:{Line} " : string.Empty;
        var where = (Passes.Length > 0 ? $" [{Passes}]" : string.Empty) + (RanFrom.Length > 0 ? $" (run from {RanFrom})" : string.Empty);
        var compared = Expected == null ? string.Empty : $" (expected: {Expected}, actual: {Actual})";
        return $"{scenario}{Name}{where} {(Passed ? "PASS" : "FAIL")} {Detail}{compared}".TrimEnd();
    }

    public static RunStep Pass(string name)
    {
        return new RunStep { Name = name, Passed = true };
    }

    public static RunStep Fail(string name, string detail)
    {
        return new RunStep { Name = name, Passed = false, Detail = detail };
    }
}
