using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks Imui reads the player's mouse, not one a step held.</summary>
[Step("expect-player-mouse")]
public sealed class ExpectPlayerMouse : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        var imui = window.ImuiMouse;
        if (imui != window.PlayerMouse)
            context.Fail("Imui doesn't read the player's mouse", window.PlayerMouse.ToString(), imui.ToString());
        else
            context.Detail = $"Imui reads the player's mouse, at {imui}";

        yield break;
    }
}
