using System.Collections;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// One step of a scenario. Each <see cref="ArgAttribute"/> property is one of its arguments.
/// </summary>
public abstract class Step
{
    public abstract IEnumerator Run(IStepContext context);
}
