using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Holds keys with <c>context.Input</c>, until the tester's own step (#20) does this.</summary>
[Step("hold-keys")]
public sealed class HoldKeys : Step
{
    /// <summary>The keys.</summary>
    [Arg(Primary = true)]
    public required Keys Keys { get; set; }

    /// <summary>How long.</summary>
    [Arg]
    public required Seconds For { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        return context.Input.Hold(Keys, For);
    }
}
