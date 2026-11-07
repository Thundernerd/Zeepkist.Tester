using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Counts each hook in the plugin's service, for <see cref="ExpectHooks"/> to check.
/// </summary>
public sealed class CountingHooks : IScenarioHooks
{
    public void RunStarted(IRunContext run)
    {
        run.Get<HookCounts>().RunStarted++;
    }

    public IEnumerator BeforeScenario(IScenarioContext scenario)
    {
        scenario.Get<HookCounts>().BeforeScenario++;
        yield break;
    }

    public IEnumerator AfterScenario(IScenarioContext scenario)
    {
        scenario.Get<HookCounts>().AfterScenario++;
        yield break;
    }
}
