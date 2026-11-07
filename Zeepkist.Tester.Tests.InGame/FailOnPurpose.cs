using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Fails, to check what a run does then.</summary>
[Step("fail-on-purpose")]
public sealed class FailOnPurpose : Step
{
    /// <summary>What the results say.</summary>
    [Arg(Primary = true)]
    public required string Why { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        context.Fail(Why);
        yield break;
    }
}
