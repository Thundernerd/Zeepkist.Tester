using System.Globalization;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A length of time. A scenario writes it as <c>2</c>, <c>0.5</c> or <c>500ms</c>.
/// </summary>
public readonly struct Seconds
{
    /// <summary>
    /// The form with a unit. A plain number is also a length of time, so it doesn't have to match this.
    /// </summary>
    public const string Pattern = @"^\d+(\.\d+)?(ms|s)$";

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
