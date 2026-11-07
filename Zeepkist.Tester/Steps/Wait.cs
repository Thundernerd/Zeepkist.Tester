using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Waits, such as for the level to run a little before a screenshot.</summary>
[Step("wait")]
[Example("- wait: 2")]
[Example("- wait: 500ms")]
public sealed class Wait : Step
{
    /// <summary>How long to wait.</summary>
    [Arg(Primary = true)]
    public required Seconds Seconds { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        yield return context.WaitSeconds(Seconds);
    }
}
