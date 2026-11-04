namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Keys to press, in the order they're written, such as <c>ctrl+space escape</c>.
/// </summary>
public readonly struct Keys
{
    private const string Key = @"(?:space|enter|tab|escape|backspace|delete|up|down|left|right|home|end|pageup|pagedown|[a-z0-9/\.,;'\[\]=`-])";
    private const string Chord = @"(?:(?:shift|ctrl|alt|cmd)\+)*" + Key;

    public const string Pattern = "^" + Chord + "(?: " + Chord + ")*$";

    public Keys(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static implicit operator Keys(string value)
    {
        return new Keys(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
