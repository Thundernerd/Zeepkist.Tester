namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// One item of a <c>steps:</c> list.
/// </summary>
internal abstract class ScenarioNode
{
    protected ScenarioNode(SourceLocation location)
    {
        Location = location;
    }

    public SourceLocation Location { get; }
}
