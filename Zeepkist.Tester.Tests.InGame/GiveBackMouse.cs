using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Moves the mouse somewhere the player's isn't, gives it back, and checks Imui reads the player's again.</summary>
[Step("give-back-mouse")]
public sealed class GiveBackMouse : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        var player = window.PlayerMouse;
        yield return context.Input.MoveMouse(window.SliderLeft);
        var moved = window.ImuiMouse;
        context.Input.GiveBack();
        yield return null;
        var after = window.ImuiMouse;
        if (after != window.PlayerMouse)
            context.Fail("Imui doesn't read the player's mouse again", window.PlayerMouse.ToString(), after.ToString());
        else
            context.Detail = $"The player's mouse at {player}, moved to {moved}, back at {after}";
    }
}
