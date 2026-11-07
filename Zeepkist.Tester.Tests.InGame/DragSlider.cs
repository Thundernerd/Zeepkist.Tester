using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Drags the window's slider halfway from its left end, which should move it past a quarter.</summary>
[Step("drag-slider")]
public sealed class DragSlider : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        var half = window.SliderWidth / 2;
        yield return context.Input.Drag(window.SliderLeft, [new DragMove { By = half, Over = 5, Sideways = true }]);
        if (window.Slider is > 25 and < 75)
            context.Pass($"The slider is at {window.Slider}", null, null);
        else
            context.Fail("The drag didn't move the slider to about halfway", "about 50", window.Slider.ToString(CultureInfo.InvariantCulture));
    }
}
