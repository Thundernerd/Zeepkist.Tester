namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// An argument's value as the file wrote it, which is converted to the argument's type each time a step is bound.
/// </summary>
internal abstract class ArgumentValue
{
    protected ArgumentValue(SourceLocation location)
    {
        Location = location;
    }

    public SourceLocation Location { get; }
}
