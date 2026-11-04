using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// How a scenario writes an enum member, when it isn't the member's name in kebab case.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class ArgValueAttribute : Attribute
{
    public ArgValueAttribute(string value)
    {
        Value = value;
    }

    public string Value { get; }
}
