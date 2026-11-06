using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The step needs all of these states. The harness fails it without running it when one doesn't hold.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequiresAttribute : Attribute
{
    public RequiresAttribute(params Type[] states)
    {
        States = states;
    }

    public Type[] States { get; }
}
