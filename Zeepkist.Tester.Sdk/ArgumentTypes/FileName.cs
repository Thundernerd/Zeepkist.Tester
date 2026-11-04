namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A name for a file, such as a screenshot's, without its extension.
/// </summary>
public readonly struct FileName
{
    public const string Pattern = "^[A-Za-z0-9_-]+$";

    public FileName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static implicit operator FileName(string value)
    {
        return new FileName(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
