using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The step needs at least one of these states. The harness fails it without running it when none holds.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequiresAnyAttribute : Attribute
{
    public RequiresAnyAttribute(params Type[] states)
    {
        States = states;
    }

    public Type[] States { get; }
}
