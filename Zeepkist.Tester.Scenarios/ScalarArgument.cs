namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A single value, as its YAML text.
/// </summary>
internal sealed class ScalarArgument : ArgumentValue
{
    public ScalarArgument(SourceLocation location, string text)
        : base(location)
    {
        Text = text;
    }

    public string Text { get; }
}
