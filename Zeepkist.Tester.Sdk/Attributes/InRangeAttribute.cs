using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The lowest and highest value a number argument can have, both included.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class InRangeAttribute : Attribute
{
    public InRangeAttribute(double minimum, double maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public double Minimum { get; }

    public double Maximum { get; }
}
