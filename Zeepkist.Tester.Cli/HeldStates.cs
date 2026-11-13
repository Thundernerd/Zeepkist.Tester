using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// The states that hold as a scenario is followed offline, as far as its steps declare what they enter and leave.
/// </summary>
internal sealed class HeldStates
{
    private readonly IReadOnlyDictionary<string, StateDescriptor> _states;
    private readonly HashSet<string> _held;

    /// <param name="states">Every state of the registry, by full name.</param>
    /// <param name="held">The states that hold where the scenario starts.</param>
    public HeldStates(IReadOnlyDictionary<string, StateDescriptor> states, IEnumerable<string> held)
    {
        _states = states;
        _held = [.. held];
    }

    /// <summary>
    /// The entries of the step's requirements that don't hold, each as the states to name.
    /// </summary>
    public IEnumerable<IReadOnlyList<StateDescriptor>> Unmet(StepDescriptor step)
    {
        return step.Requires
            .Select(alternatives => NestedStates.Unmet(alternatives, OuterOf, _held.Contains))
            .Where(unmet => unmet != null)
            .Select(unmet => (IReadOnlyList<StateDescriptor>)unmet.Select(name => _states[name]).ToList());
    }

    /// <summary>
    /// Leaves what the step leaves, and every state within those, then enters what it enters.
    /// </summary>
    public void Apply(StepDescriptor step)
    {
        foreach (var state in step.Leaves)
            Leave(state);

        foreach (var state in step.Enters)
            _held.Add(state);
    }

    /// <summary>
    /// The state the named one is within, or null. Only looked up once the rule gets to it, so an alternative after one
    /// that holds needn't be a state the registry knows.
    /// </summary>
    private string OuterOf(string name)
    {
        return _states[name].Within;
    }

    private void Leave(string name)
    {
        _held.Remove(name);
        foreach (var inner in _states.Values.Where(state => state.Within == name))
            Leave(inner.FullName);
    }
}
