using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Checks the states a step requires before it runs. This is the check that counts: the offline one only follows what steps declare.
/// </summary>
internal static class StepPreconditions
{
    private static readonly StepDescriptorBuilder _builder = new StepDescriptorBuilder();

    /// <summary>
    /// Runs the step if the states it requires hold, and otherwise stops without running it.
    /// </summary>
    /// <exception cref="StepFailedException">A required state doesn't hold. The message says which, and how to get into it.</exception>
    /// <exception cref="StepDefinitionException">The step or a state it names breaks one of the registry's rules.</exception>
    public static IEnumerator Run(Step step, IStepContext context)
    {
        Check(step.GetType(), context);
        yield return step.Run(context);
    }

    private static void Check(Type stepType, IStepContext context)
    {
        // Also makes sure there's no cycle in Within to follow
        var step = _builder.Build(stepType);

        foreach (var alternatives in Required(stepType))
        {
            var unmet = alternatives.Select(state => OutermostUnmet(state, context)).TakeWhile(state => state != null).ToList();
            if (unmet.Count == alternatives.Length)
                throw new StepFailedException(Message(step, unmet.Distinct().Select(_builder.BuildState).ToList()));
        }
    }

    /// <summary>
    /// Each entry holds when any one of its states does.
    /// </summary>
    private static IEnumerable<Type[]> Required(Type stepType)
    {
        return stepType.GetCustomAttributes<RequiresAttribute>()
            .SelectMany(attribute => attribute.States)
            .Select(state => new[] { state })
            .Concat(stepType.GetCustomAttributes<RequiresAnyAttribute>().Select(attribute => attribute.States));
    }

    /// <summary>
    /// Leaving a state leaves every state within it, so a state only holds when the ones around it do. Null when it holds,
    /// and otherwise the state to name, because that's the one to get into first.
    /// </summary>
    private static Type OutermostUnmet(Type state, IStepContext context)
    {
        var within = state.GetCustomAttribute<StateAttribute>().Within;
        var outer = within == null ? null : OutermostUnmet(within, context);
        if (outer != null)
            return outer;

        return ((IState)Activator.CreateInstance(state)).IsMet(context) ? null : state;
    }

    private static string Message(StepDescriptor step, List<StateDescriptor> unmet)
    {
        var names = string.Join(" or ", unmet.Select(state => state.FullName));

        // A hint is written for the step reference too, where backticks mean code. In a run's results they'd be noise
        var hints = string.Join(" ", unmet.Select(state => state.Hint).Where(hint => hint != null)).Replace("`", string.Empty);
        return hints.Length == 0 ? $"{step.FullName} needs {names}." : $"{step.FullName} needs {names}: {hints}";
    }
}
