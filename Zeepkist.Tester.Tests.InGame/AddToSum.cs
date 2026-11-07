using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Adds a whole number to what the scenario has added up so far.</summary>
[Step("add")]
public sealed class AddToSum : Step
{
    /// <summary>The number, which can be a <c>${name}</c>.</summary>
    [Arg(Primary = true)]
    public required int Number { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        context.Scenario<Sum>().Total += Number;
        yield break;
    }
}
