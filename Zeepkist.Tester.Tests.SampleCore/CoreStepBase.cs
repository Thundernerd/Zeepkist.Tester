using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleCore;

/// <summary>A base type for steps in other assemblies, so a test can read such a step where this assembly is missing.</summary>
public abstract class CoreStepBase : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
