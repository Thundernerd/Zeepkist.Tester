using System.Collections.Generic;

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
    /// What its state's argument was written as, or null without one.
    /// </summary>
    public string Value => State.Argument != null && Arguments.TryGetValue(State.Argument, out var value) ? value : null;

    /// <summary>
    /// The level file to open, or null for a new level.
    /// </summary>
    /// <param name="folder">What the file is written relative to: a scenario's own folder, or the <c>scenarios</c> folder for a C# test.</param>
    public string LevelPath(string folder)
    {
        return State == StartState.Editor && Value != null ? ScenarioPaths.InFolder(folder, Value) : null;
    }
}
