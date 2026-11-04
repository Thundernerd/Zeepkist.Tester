using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A line of a scenario that uses the step, for its entry in the step reference.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ExampleAttribute : Attribute
{
    public ExampleAttribute(string yaml)
    {
        Yaml = yaml;
    }

    public string Yaml { get; }
}
