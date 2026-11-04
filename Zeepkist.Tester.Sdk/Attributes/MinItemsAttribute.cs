using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The fewest items a list argument can have.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class MinItemsAttribute : Attribute
{
    public MinItemsAttribute(int count)
    {
        Count = count;
    }

    public int Count { get; }
}
