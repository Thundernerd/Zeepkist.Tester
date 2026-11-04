using System.Globalization;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A length of time. A scenario writes it as <c>2</c>, <c>0.5</c> or <c>500ms</c>.
/// </summary>
public readonly struct Seconds
{
    public Seconds(float value)
    {
        Value = value;
    }

    public float Value { get; }

    public static implicit operator Seconds(float value)
    {
        return new Seconds(value);
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
