using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The step gets the scenario into these states.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class EntersAttribute : Attribute
{
    public EntersAttribute(params Type[] states)
    {
        States = states;
    }

    public Type[] States { get; }
}
