using System.Collections.Generic;

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

    /// <summary>
    /// Each <c>run:</c> in the nodes and the bodies of their <c>for</c>s and <c>repeat</c>s, in the order they're written.
    /// The files they run aren't read.
    /// </summary>
    public static IEnumerable<RunNode> RunsIn(IReadOnlyList<ScenarioNode> nodes)
    {
        foreach (var node in nodes)
        {
            var runs = node switch
            {
                ForNode loop => RunsIn(loop.Steps),
                RepeatNode repeat => RunsIn(repeat.Steps),
                RunNode run => [run],
                _ => [],
            };

            foreach (var run in runs)
                yield return run;
        }
    }
}
