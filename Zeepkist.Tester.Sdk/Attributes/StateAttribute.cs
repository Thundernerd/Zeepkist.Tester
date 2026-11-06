using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Makes an <see cref="IState"/> available to steps' <see cref="RequiresAttribute"/>, <see cref="RequiresAnyAttribute"/>, <see cref="EntersAttribute"/> and <see cref="LeavesAttribute"/>, under a kebab-case name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StateAttribute : Attribute
{
    public StateAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>
    /// Defaults to the namespace of the assembly's <see cref="TestPluginAttribute"/>.
    /// </summary>
    public string Namespace { get; set; }

    /// <summary>
    /// The state this one nests in. Leaving that state leaves this one too.
    /// </summary>
    public Type Within { get; set; }

    /// <summary>
    /// What to add to a scenario to get into this state, for the message when a step needs it.
    /// </summary>
    public string Hint { get; set; }
}
