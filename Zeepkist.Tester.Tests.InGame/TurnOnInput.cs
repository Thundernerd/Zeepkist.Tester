using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Turns on what <c>turn-off-input</c> turned off.</summary>
[Step("turn-on-input")]
public sealed class TurnOnInput : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        context.Get<InputOff>().TurnOnAll();
        yield return null;
    }
}
