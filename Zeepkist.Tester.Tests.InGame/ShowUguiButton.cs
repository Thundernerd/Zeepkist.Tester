using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Shows a uGUI button in the middle of the screen, above the game's own.</summary>
[Step("show-ugui-button")]
public sealed class ShowUguiButton : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        context.Get<UguiButton>().Show();

        // Its canvas lays it out as it draws
        yield return null;
    }
}
