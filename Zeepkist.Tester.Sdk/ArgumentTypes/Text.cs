namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Several lines of text, such as a Script's text.
/// </summary>
public readonly struct Text
{
    public Text(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static implicit operator Text(string value)
    {
        return new Text(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
