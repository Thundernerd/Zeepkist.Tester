using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleCore;

/// <summary>A step as the tester ships them: with no namespace, in an assembly with no <see cref="TestPluginAttribute"/>.</summary>
[Step("core-step")]
public sealed class CoreStep : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
