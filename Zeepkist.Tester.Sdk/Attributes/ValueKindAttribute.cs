using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Makes a class a kind of named value, which <see cref="Name{T}"/> defines and <see cref="Ref{T}"/> uses.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValueKindAttribute : Attribute
{
    public ValueKindAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// What a scenario's checks and errors call this kind, such as <c>block</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The public string property that <c>${name}</c> writes. Without it, <c>${name}</c> is an error for this kind.
    /// </summary>
    public string Substitute { get; set; }
}
