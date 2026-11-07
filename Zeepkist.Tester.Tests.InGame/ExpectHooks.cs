using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks how often each hook ran, from the plugin's service.</summary>
[Step("expect-hooks")]
public sealed class ExpectHooks : Step
{
    /// <summary>How often <c>RunStarted</c> ran.</summary>
    [Arg]
    public required int RunStarted { get; set; }

    /// <summary>How often <c>BeforeScenario</c> ran.</summary>
    [Arg]
    public required int Before { get; set; }

    /// <summary>How often <c>AfterScenario</c> ran.</summary>
    [Arg]
    public required int After { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var expected = new HookCounts { RunStarted = RunStarted, BeforeScenario = Before, AfterScenario = After }.ToString();
        var actual = context.Get<HookCounts>().ToString();
        if (expected == actual)
            context.Pass("The hooks ran as often as expected.", expected, actual);
        else
            context.Fail("The hooks ran another number of times.", expected, actual);

        yield break;
    }
}
