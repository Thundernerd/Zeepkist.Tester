using System.Collections;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Starts test mode, and waits until the round starts and the player can drive. The level needs a start block.</summary>
[Step("test-mode")]
[Example("- test-mode")]
[Requires(typeof(InEditor))]
[Leaves(typeof(InEditor))]
[Enters(typeof(InTestMode))]
public sealed class TestMode : Step
{
    private const float StartSeconds = 60;

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        if (!central.Blocks().Any(block => block.isStart))
        {
            context.Fail("The level has no start block. Add place-start first.");
            yield break;
        }

        var entriesBefore = context.Game.TestModeEntries;
        var roundsBefore = context.Game.RoundStarts;

        // Does nothing in a level without a start block
        central.testMap.TestMap();
        yield return context.WaitUntil(() => context.Game.TestModeEntries > entriesBefore, new Seconds(StartSeconds), "test mode to start");
        yield return context.WaitUntil(() => context.Game.RoundStarts > roundsBefore, new Seconds(StartSeconds), "the round to start in test mode");
    }
}
