using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Clicks the uGUI button, which should click it once, then presses it and lets go beside it, which shouldn't.
/// </summary>
[Step("click-ugui-button")]
public sealed class ClickUguiButton : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var button = context.Get<UguiButton>();
        yield return context.Input.Click(button.Center);
        if (button.Clicks != 1)
        {
            context.Fail("A click on the button didn't click it once", "1", button.Clicks.ToString(CultureInfo.InvariantCulture));
            yield break;
        }

        yield return context.Input.Drag(button.Center, [new DragMove { By = button.Width, Over = 2, Sideways = true }]);
        if (button.Clicks != 1)
            context.Fail("Letting go beside the button clicked it", "1", button.Clicks.ToString(CultureInfo.InvariantCulture));
    }
}
