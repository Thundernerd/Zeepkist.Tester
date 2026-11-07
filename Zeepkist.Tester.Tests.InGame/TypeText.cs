using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Types with <c>context.Input</c>, until the tester's own step (#20) does this.</summary>
[Step("type-text")]
public sealed class TypeText : Step
{
    /// <summary>What to type.</summary>
    [Arg(Primary = true)]
    public required string Text { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        return context.Input.Type(Text);
    }
}
