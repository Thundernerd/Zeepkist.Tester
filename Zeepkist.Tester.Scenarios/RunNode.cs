using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// <c>run: file</c>, with the named values its <c>with:</c> passes in. Those are single values, so they can use <c>${name}</c>.
/// </summary>
internal sealed class RunNode : ScenarioNode
{
    public RunNode(SourceLocation location, string file, IReadOnlyDictionary<string, ScalarArgument> with)
        : base(location)
    {
        File = file;
        With = with;
    }

    public string File { get; }

    public IReadOnlyDictionary<string, ScalarArgument> With { get; }
}
