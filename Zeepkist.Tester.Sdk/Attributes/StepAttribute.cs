using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Makes a <see cref="Step"/> available to scenarios under a kebab-case name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StepAttribute : Attribute
{
    public StepAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>
    /// Defaults to the namespace of the assembly's <see cref="TestPluginAttribute"/>.
    /// </summary>
    public string Namespace { get; set; }
}
