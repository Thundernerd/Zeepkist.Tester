using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Presses keys with <c>context.Input</c>, until the tester's own step (#20) does this.</summary>
[Step("press-keys")]
public sealed class PressKeys : Step
{
    /// <summary>The keys.</summary>
    [Arg(Primary = true)]
    public required Keys Keys { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        return context.Input.Press(Keys);
    }
}
