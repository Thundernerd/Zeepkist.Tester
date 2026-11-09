using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Hides the uGUI button.</summary>
[Step("hide-ugui-button")]
public sealed class HideUguiButton : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        context.Get<UguiButton>().Hide();
        yield break;
    }
}
