namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// The service the plugin gives: how often each hook ran.
/// </summary>
public sealed class HookCounts
{
    public int RunStarted { get; set; }

    public int BeforeScenario { get; set; }

    public int AfterScenario { get; set; }

    public override string ToString()
    {
        return $"run started {RunStarted}, before {BeforeScenario}, after {AfterScenario}";
    }
}
