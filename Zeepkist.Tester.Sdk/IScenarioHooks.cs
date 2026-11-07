using System.Collections;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// What a mod does around scenarios, such as putting back what the last one changed. Register it with
/// <see cref="TestPluginRegistration.Hooks"/>. Each plugin's hooks run in the order they were registered.
/// </summary>
public interface IScenarioHooks
{
    /// <summary>
    /// Once, before the first scenario walks to where it starts.
    /// </summary>
    public void RunStarted(IRunContext run);

    /// <summary>
    /// Before each scenario but the first, and before it walks to where it starts, so the game is still where the last one left it.
    /// </summary>
    public IEnumerator BeforeScenario(IScenarioContext scenario);

    /// <summary>
    /// After each scenario, also after a failed one or one that never got to where it starts.
    /// </summary>
    public IEnumerator AfterScenario(IScenarioContext scenario);
}
