using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Tests.SampleCore;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>Needs and changes states of this assembly and of the tester's.</summary>
[Step("swap-panel")]
[Requires(typeof(CoreState))]
[RequiresAny(typeof(PanelOpen), typeof(CoreState))]
[Enters(typeof(PanelOpen))]
[Leaves(typeof(CoreState))]
public sealed class SwapPanel : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
