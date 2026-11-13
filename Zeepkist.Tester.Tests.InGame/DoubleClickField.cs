using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Clicks the start of the window's text field twice in a row, which should select its first word.</summary>
[Step("double-click-field")]
public sealed class DoubleClickField : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        yield return context.Input.Click(window.FieldStart);
        yield return context.Input.Click(window.FieldStart);
    }
}
