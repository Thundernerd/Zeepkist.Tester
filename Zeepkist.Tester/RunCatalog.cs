namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What a run can run: the steps scenarios and C# tests use, and the C# tests to choose from.
/// </summary>
internal sealed class RunCatalog
{
    public RunCatalog(StepTypes steps, InGameTests tests)
    {
        Steps = steps;
        Tests = tests;
    }

    public StepTypes Steps { get; }

    public InGameTests Tests { get; }
}
