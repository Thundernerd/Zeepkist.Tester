using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Moves the mouse onto the slider, checks Imui finds it there, and leaves it there for the harness to give back.</summary>
[Step("hold-mouse")]
public sealed class HoldMouse : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        yield return context.Input.MoveMouse(window.SliderLeft);
        if (!window.ImuiMouseAtSliderLeft)
            context.Fail("Imui doesn't find the mouse where it was moved", window.SliderLeftInImui.ToString(), window.ImuiMouse.ToString());
        else
            context.Detail = $"The mouse is held at {window.ImuiMouse}";
    }
}
