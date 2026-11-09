using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Moves the mouse onto the slider, checks Imui finds it there, and leaves it there for the harness to give back.</summary>
[Step("hold-mouse")]
public sealed class HoldMouse : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        yield return context.Input.MoveMouse(window.SliderLeft);
        var held = window.ImuiMouse;
        if (Vector2.Distance(held, window.SliderLeftInImui) > 0.5f)
            context.Fail("Imui doesn't find the mouse where it was moved", window.SliderLeftInImui.ToString(), held.ToString());
        else
            context.Detail = $"The mouse is held at {held}";
    }
}
