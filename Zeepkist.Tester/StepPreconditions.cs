using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TNRD.Zeepkist.Tester.Scenarios;
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
            var unmet = NestedStates.Unmet(alternatives, Within, state => ((IState)Activator.CreateInstance(state)).IsMet(context));
            if (unmet != null)
                throw new StepFailedException(ScenarioMessages.Needs(step, unmet.Select(_builder.BuildState)));
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

    private static Type Within(Type state)
    {
        return state.GetCustomAttribute<StateAttribute>().Within;
    }
}
