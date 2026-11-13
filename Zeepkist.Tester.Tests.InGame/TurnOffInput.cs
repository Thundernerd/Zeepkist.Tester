using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Turns off one of the game's input categories through ZeepSDK, as a mod does while its text field has focus, until
/// <c>turn-on-input</c> or the end of the scenario.
/// </summary>
[Step("turn-off-input")]
public sealed class TurnOffInput : Step
{
    /// <summary>The category.</summary>
    [Arg(Primary = true)]
    public required InputCategory Category { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        context.Get<InputOff>().TurnOff(Category);

        // The maps are off from Rewired's next update
        yield return null;
    }
}
