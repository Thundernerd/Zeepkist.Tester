using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// What's said about a mistake that both the harness and <c>ztest check</c> find, so a check reads like the failure it predicts.
/// </summary>
internal static class ScenarioMessages
{
    public static string NotDefined(string name)
    {
        return $"The name '{name}' isn't defined by an earlier step.";
    }

    public static string AlreadyDefined(string name, string kind)
    {
        return $"The name '{name}' is already defined, as a {kind}.";
    }

    public static string WrongKind(string name, string kind, string expected)
    {
        return $"The name '{name}' is a {kind}, not a {expected}.";
    }

    public static string NoSubstituteMember(string name, string kind)
    {
        return $"${{{name}}} can't be written: a {kind} has no Substitute member.";
    }

    /// <summary>
    /// When the states a step needs don't hold.
    /// </summary>
    public static string Needs(StepDescriptor step, IEnumerable<StateDescriptor> unmet)
    {
        var states = unmet.ToList();
        var names = string.Join(" or ", states.Select(state => state.FullName));

        // A hint is written for the step reference too, where backticks mean code. In a message they'd be noise
        var hints = string.Join(" ", states.Select(state => state.Hint).Where(hint => hint != null)).Replace("`", string.Empty);
        return hints.Length == 0 ? $"{step.FullName} needs {names}." : $"{step.FullName} needs {names}: {hints}";
    }
}
