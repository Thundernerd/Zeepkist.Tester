using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Makes a property an argument of its step or nested class.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class ArgAttribute : Attribute
{
    /// <summary>
    /// Lets a scenario give this argument alone, as the step's plain value: <c>- wait: 2</c>.
    /// </summary>
    public bool Primary { get; set; }

    /// <summary>
    /// The value when a scenario leaves the argument out.
    /// </summary>
    public object Default { get; set; }

    /// <summary>
    /// The YAML key, when it isn't the property's name in kebab case.
    /// </summary>
    public string Name { get; set; }
}
