using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Clicks the start of the window's text field twice in a row, which should select its first word.</summary>
[Step("double-click-field")]
public sealed class DoubleClickField : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        // A click takes 3 frames, so the second press is a double-click only above about 15 frames a second, as a player's is
        var window = context.Get<InputWindow>();
        yield return context.Input.Click(window.FieldStart);
        yield return context.Input.Click(window.FieldStart);
    }
}
