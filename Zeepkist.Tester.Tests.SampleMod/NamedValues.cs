using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>Defines one named value and uses another.</summary>
[Step("named-values")]
public sealed class NamedValues : Step
{
    /// <summary>A name for the new block.</summary>
    [Arg]
    public Name<SampleBlock>? As { get; set; }

    /// <summary>The block to start from.</summary>
    [Arg]
    public Ref<SampleBlock> From { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
