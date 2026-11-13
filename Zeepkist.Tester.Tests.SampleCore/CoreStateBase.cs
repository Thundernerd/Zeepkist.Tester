using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleCore;

/// <summary>A base type for states in other assemblies, so a test can read such a state where this assembly is missing.</summary>
public abstract class CoreStateBase : IState
{
    public bool IsMet(IStepContext context)
    {
        return true;
    }
}
