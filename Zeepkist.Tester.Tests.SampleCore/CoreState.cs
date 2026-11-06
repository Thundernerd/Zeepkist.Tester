using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleCore;

/// <summary>A state in an assembly a mod references, for its states to nest in. It has a namespace because this isn't always the tester in the tests.</summary>
[State("core-state", Namespace = "core", Hint = "Add `core-step` first.")]
public sealed class CoreState : IState
{
    public bool IsMet(IStepContext context)
    {
        return true;
    }
}
