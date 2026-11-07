using System.Collections.Generic;
using System.IO;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Where a scenario starts: <c>main-menu</c>, <c>editor</c>, <c>editor: { level: file }</c> or <c>free-play: { uid: … }</c>.
/// </summary>
internal sealed class ScenarioStart
{
    public ScenarioStart(StartState state, IReadOnlyDictionary<string, string> arguments, SourceLocation location = null)
    {
        State = state;
        Arguments = arguments;
        Location = location;
    }

    /// <summary>
    /// Where a scenario whose header has no <c>start:</c> starts.
    /// </summary>
    public static ScenarioStart Default { get; } = new ScenarioStart(StartState.Editor, new Dictionary<string, string>());

    public StartState State { get; }

    public IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary>
    /// Null for <see cref="Default"/>, which isn't written anywhere.
    /// </summary>
    public SourceLocation Location { get; }

    /// <summary>
    /// The level file to open, which is written relative to the scenario's file, or null for a new level.
    /// </summary>
    public string LevelPath(string scenarioFile)
    {
        return State == StartState.Editor && Arguments.TryGetValue(StartState.Editor.Argument, out var level)
            ? Path.Combine(Path.GetDirectoryName(scenarioFile), level.Replace('/', Path.DirectorySeparatorChar))
            : null;
    }
}
