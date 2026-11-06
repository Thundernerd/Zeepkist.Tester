namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Several lines of text, such as a Script's text.
/// </summary>
public readonly struct TextBlock
{
    public TextBlock(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static implicit operator TextBlock(string value)
    {
        return new TextBlock(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
