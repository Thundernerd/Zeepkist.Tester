using System;
using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// The rule for a step's required states, which the harness checks on state types and <c>ztest check</c> on descriptors:
/// leaving a state leaves every state within it, so a state only holds when the ones around it do.
/// </summary>
internal static class NestedStates
{
    /// <summary>
    /// Whether one entry of a step's requirements fails, which it does when none of its states hold. Asks whether a state
    /// holds only until one does.
    /// </summary>
    /// <param name="alternatives">The states of the entry, any one of which is enough.</param>
    /// <param name="within">The state a state is within, or null.</param>
    /// <param name="holds">Whether a state holds by itself, without the ones around it.</param>
    /// <returns>The state to name for each alternative, without repeats, or null when the entry holds.</returns>
    public static IReadOnlyList<TState> Unmet<TState>(IReadOnlyCollection<TState> alternatives, Func<TState, TState> within, Func<TState, bool> holds)
        where TState : class
    {
        var unmet = alternatives.Select(state => OutermostUnmet(state, within, holds)).TakeWhile(state => state != null).ToList();
        return unmet.Count == alternatives.Count ? unmet.Distinct().ToList() : null;
    }

    /// <summary>
    /// Null when the state holds, and otherwise the state to name, because that's the one to get into first.
    /// </summary>
    private static TState OutermostUnmet<TState>(TState state, Func<TState, TState> within, Func<TState, bool> holds)
        where TState : class
    {
        var outer = within(state) is { } around ? OutermostUnmet(around, within, holds) : null;
        return outer ?? (holds(state) ? null : state);
    }
}
