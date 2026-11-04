using System.Globalization;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A distance on the screen, which can be negative.
/// </summary>
public readonly struct Pixels
{
    public Pixels(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static implicit operator Pixels(int value)
    {
        return new Pixels(value);
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
