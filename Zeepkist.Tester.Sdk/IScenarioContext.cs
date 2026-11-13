namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// What's there for one scenario, including its <c>run:</c> files, or for one C# test.
/// </summary>
public interface IScenarioContext : IRunContext
{
    /// <summary>
    /// The scenario's file, relative to the run's scenarios folder and without <c>.ztest.yaml</c>, such as <c>compile/errors</c>.
    /// For a C# test, the test's name, such as <c>WindowTests.Opens</c>.
    /// </summary>
    public string ScenarioName { get; }

    /// <summary>
    /// State a mod keeps for the length of one scenario: made on first use, and dropped when the scenario ends.
    /// </summary>
    public T Scenario<T>()
        where T : class, new();
}
