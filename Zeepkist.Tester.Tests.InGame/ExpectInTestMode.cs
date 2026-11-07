using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using ZeepSDK.LevelEditor;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks ZeepSDK agrees the game is in test mode, past the state the tester declares.</summary>
[Step("expect-in-test-mode")]
[Requires(typeof(InTestMode))]
public sealed class ExpectInTestMode : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        if (!LevelEditorApi.IsTestingLevel)
            context.Fail("ZeepSDK says the level isn't being tested.");

        yield break;
    }
}
