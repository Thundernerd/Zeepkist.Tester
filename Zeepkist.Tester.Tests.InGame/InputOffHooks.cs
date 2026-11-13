using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Turns the input a scenario turned off on again, so one that fails with it off leaves the next scenario its keys.
/// </summary>
public sealed class InputOffHooks : IScenarioHooks
{
    public void RunStarted(IRunContext run)
    {
    }

    public IEnumerator BeforeScenario(IScenarioContext scenario)
    {
        yield break;
    }

    public IEnumerator AfterScenario(IScenarioContext scenario)
    {
        scenario.Get<InputOff>().TurnOnAll();
        yield break;
    }
}
