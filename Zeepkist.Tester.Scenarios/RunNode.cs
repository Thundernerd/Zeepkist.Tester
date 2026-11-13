using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Where <see cref="With"/> doesn't pass what the file's <c>with:</c> header says it takes: each name it leaves out, at
    /// the run, and then each it passes that the file doesn't take, at its value. None when the file has no header.
    /// </summary>
    public IEnumerable<ScenarioException> Mismatches(Scenario target)
    {
        if (target.Takes == null)
            return [];

        var missing = target.Takes.Where(name => !With.ContainsKey(name))
            .Select(name => new ScenarioException(Location, ScenarioMessages.RunWithMissing(File, name)));
        var unknown = With.Where(entry => !target.Takes.Contains(entry.Key))
            .Select(entry => new ScenarioException(entry.Value.Location, ScenarioMessages.RunWithUnknown(File, entry.Key, target.Takes)));
        return missing.Concat(unknown);
    }
}
