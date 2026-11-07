using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Clicks the window's text field, which should take the focus.</summary>
[Step("click-field")]
public sealed class ClickField : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        yield return context.Input.Click(window.FieldCenter);
        if (!window.FieldFocused)
            context.Fail("The click didn't focus the field", "focused", "not focused");
    }
}
