using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Quits test mode the way a player does, and waits until the editor has loaded the level again, blocks and all.</summary>
[Step("back-to-editor")]
[Example("- back-to-editor")]
[Requires(typeof(InTestMode))]
[Leaves(typeof(InTestMode))]
[Enters(typeof(InEditor))]
public sealed class BackToEditor : Step
{
    private const float LoadSeconds = 60;

    public override IEnumerator Run(IStepContext context)
    {
        var pauseMenu = RoundControls.FindPauseMenu();
        if (pauseMenu == null)
            throw new StepFailedException("The level has no pause menu to quit test mode from.");

        var loadsBefore = context.Game.LevelLoads;
        yield return RoundControls.Quit(pauseMenu);
        yield return context.WaitUntil(() => RoundControls.IsLevelBack(context.Game, loadsBefore), new Seconds(LoadSeconds), "the editor to load the level again");
    }
}
