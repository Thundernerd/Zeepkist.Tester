using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Marks a mod's test plugin, and gives the namespace of the steps in its assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TestPluginAttribute : Attribute
{
    public string Namespace { get; set; }
}
