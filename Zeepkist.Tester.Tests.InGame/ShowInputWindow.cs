using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Shows the window, and waits until it's drawn.</summary>
[Step("show-input-window")]
public sealed class ShowInputWindow : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var window = context.Get<InputWindow>();
        window.Open = true;
        yield return context.WaitUntil(() => window.Drawn, 5, "the input window to be drawn");
        if (window.Backends != 1)
            context.Fail("The driver hands input to one Imui backend", "1", window.Backends.ToString(CultureInfo.InvariantCulture));
    }
}
