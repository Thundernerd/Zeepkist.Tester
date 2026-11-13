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
    /// <param name="start">The state the scenario starts in.</param>
    public HeldStates(IReadOnlyDictionary<string, StateDescriptor> states, string start)
    {
        _states = states;
        _held = [start];
    }

    /// <summary>
    /// The entries of the step's requirements that don't hold, each as the states to name.
    /// </summary>
    public IEnumerable<IReadOnlyList<StateDescriptor>> Unmet(StepDescriptor step)
    {
        return step.Requires
            .Select(alternatives => NestedStates.Unmet(alternatives.Select(name => _states[name]).ToList(), Within, state => _held.Contains(state.FullName)))
            .Where(unmet => unmet != null);
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

    private StateDescriptor Within(StateDescriptor state)
    {
        return state.Within == null ? null : _states[state.Within];
    }

    private void Leave(string name)
    {
        _held.Remove(name);
        foreach (var inner in _states.Values.Where(state => state.Within == name))
            Leave(inner.FullName);
    }
}
