using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

[Step("own-namespace", Namespace = "other")]
public sealed class OwnNamespace : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
