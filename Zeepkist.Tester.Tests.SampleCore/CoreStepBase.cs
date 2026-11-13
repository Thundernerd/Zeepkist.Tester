using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleCore;

/// <summary>A base for steps in other assemblies, for a step whose base type can't be found when this assembly can't.</summary>
public abstract class CoreStepBase : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
