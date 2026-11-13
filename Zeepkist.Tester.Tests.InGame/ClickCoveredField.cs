using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Puts the uGUI button over the window's text field and clicks it there, which should click the button only: Imui
/// doesn't find the mouse on the field, and the field stays unfocused. Hides the button again after.
/// </summary>
[Step("click-covered-field")]
public sealed class ClickCoveredField : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        var button = context.Get<UguiButton>();
        button.Show();
        button.MoveTo(window.FieldCenter);

        // Its canvas lays it out as it draws
        yield return null;
        yield return context.Input.MoveMouse(button.Center);
        var mouseOnField = window.ImuiMouseOnField;
        yield return context.Input.Click(button.Center);
        var clicks = button.Clicks;
        var focused = window.FieldFocused;
        button.Hide();

        if (mouseOnField)
            context.Fail("Imui found the mouse on the field under the button", "off the field", "on the field");
        else if (clicks != 1)
            context.Fail("A click on the button over the field didn't click it once", "1", clicks.ToString(CultureInfo.InvariantCulture));
        else if (focused)
            context.Fail("A click on the button over the field focused the field", "not focused", "focused");
    }
}
