using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Tests.SampleCore;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>The panel is open.</summary>
[State("panel-open", Within = typeof(CoreState), Hint = "Add `sample.open-panel` first.")]
public sealed class PanelOpen : IState
{
    public bool IsMet(IStepContext context)
    {
        return true;
    }
}
