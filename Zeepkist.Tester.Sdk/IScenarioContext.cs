namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// What's there for one scenario, including its <c>run:</c> files.
/// </summary>
public interface IScenarioContext : IRunContext
{
    /// <summary>
    /// The scenario's file name, without <c>.ztest.yaml</c>.
    /// </summary>
    public string ScenarioName { get; }

    /// <summary>
    /// State a mod keeps for the length of one scenario: made on first use, and dropped when the scenario ends.
    /// </summary>
    public T Scenario<T>()
        where T : class, new();
}
