using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks what the scenario has added up so far.</summary>
[Step("expect-sum")]
public sealed class ExpectSum : Step
{
    /// <summary>What it should come to.</summary>
    [Arg(Primary = true)]
    public required int Total { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var actual = context.Scenario<Sum>().Total;
        var expected = Total.ToString(CultureInfo.InvariantCulture);
        var written = actual.ToString(CultureInfo.InvariantCulture);
        if (actual == Total)
            context.Pass("The sum is right.", expected, written);
        else
            context.Fail("The sum is wrong.", expected, written);

        yield break;
    }
}
