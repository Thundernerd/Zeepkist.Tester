using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Hides the window.</summary>
[Step("hide-input-window")]
public sealed class HideInputWindow : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        context.Get<InputWindow>().Open = false;
        yield break;
    }
}
