using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A step as the parser, the schema and the docs see it.
/// </summary>
public sealed class StepDescriptor
{
    public required string Name { get; init; }

    /// <summary>
    /// Empty for the tester's own steps.
    /// </summary>
    public required string Namespace { get; init; }

    public required IReadOnlyList<ArgumentDescriptor> Arguments { get; init; }

    public string Documentation { get; init; }

    public IReadOnlyList<string> Examples { get; init; } = [];

    /// <summary>
    /// What the step needs, by state <see cref="StateDescriptor.FullName"/>. Every entry has to hold, and an entry holds when any one of its states does.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Requires { get; init; } = [];

    /// <summary>
    /// The states the step gets the scenario into, by <see cref="StateDescriptor.FullName"/>.
    /// </summary>
    public IReadOnlyList<string> Enters { get; init; } = [];

    /// <summary>
    /// The states the step takes the scenario out of, by <see cref="StateDescriptor.FullName"/>, which also leaves every state within them.
    /// </summary>
    public IReadOnlyList<string> Leaves { get; init; } = [];

    /// <summary>
    /// The name a scenario writes without <c>use:</c>, such as <c>wait</c> or <c>zls.compile</c>.
    /// </summary>
    public string FullName => Namespace.Length == 0 ? Name : $"{Namespace}.{Name}";

    /// <summary>
    /// What to say when the states the step needs don't hold, in the game and in <c>ztest check</c>.
    /// </summary>
    public string NeedsMessage(IEnumerable<StateDescriptor> unmet)
    {
        var states = unmet.ToList();
        var names = string.Join(" or ", states.Select(state => state.FullName));

        // A hint is written for the step reference too, where backticks mean code. In a message they'd be noise
        var hints = string.Join(" ", states.Select(state => state.Hint).Where(hint => hint != null)).Replace("`", string.Empty);
        return hints.Length == 0 ? $"{FullName} needs {names}." : $"{FullName} needs {names}: {hints}";
    }
}
