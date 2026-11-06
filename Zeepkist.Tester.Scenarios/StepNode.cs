using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A step of the registry, with its arguments in their long form, whichever way the file wrote them.
/// </summary>
internal sealed class StepNode : ScenarioNode
{
    public StepNode(SourceLocation location, StepDescriptor step, ObjectArgument arguments)
        : base(location)
    {
        Step = step;
        Arguments = arguments;
    }

    public StepDescriptor Step { get; }

    public ObjectArgument Arguments { get; }
}
