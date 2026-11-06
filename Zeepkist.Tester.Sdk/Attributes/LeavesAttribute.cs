using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The step takes the scenario out of these states, and so out of every state within them.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class LeavesAttribute : Attribute
{
    public LeavesAttribute(params Type[] states)
    {
        States = states;
    }

    public Type[] States { get; }
}
