using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// What's allowed in a registry but probably a mistake, such as a missing declaration that would make scenarios' warnings wrong.
/// </summary>
internal static class RegistryCheck
{
    public static IReadOnlyList<string> Smells(StepRegistry registry)
    {
        return registry.States.SelectMany(state => StateSmells(state, registry))
            .Concat(registry.Steps.SelectMany(StepSmells))
            .ToList();
    }

    private static IEnumerable<string> StateSmells(StateDescriptor state, StepRegistry registry)
    {
        var isEntered = ScenarioParser.StartStates.Contains(state.FullName) || registry.Steps.Any(step => step.Enters.Contains(state.FullName));
        if (!isEntered)
            yield return $"No step enters the state '{state.FullName}', so the offline check never sees it hold.";

        var isRequired = registry.Steps.Any(step => step.Requires.Any(alternatives => alternatives.Contains(state.FullName)));
        var hasInner = registry.States.Any(other => other.Within == state.FullName);
        if (!isRequired && !hasInner)
            yield return $"Nothing uses the state '{state.FullName}': no step requires it and no state is within it.";
        else if (state.Hint == null)
            yield return $"The state '{state.FullName}' has no Hint, so a step that needs it can't say how to get into it.";
    }

    private static IEnumerable<string> StepSmells(StepDescriptor step)
    {
        if (step.Documentation == null)
            yield return $"The step '{step.FullName}' has no <summary>, which its reference docs come from.";

        foreach (var smell in ArgumentSmells(step.FullName, step.Arguments))
            yield return smell;
    }

    private static IEnumerable<string> ArgumentSmells(string owner, IReadOnlyList<ArgumentDescriptor> arguments)
    {
        foreach (var argument in arguments)
        {
            var what = $"'{argument.Name}' of {owner}";
            if (argument.Documentation == null)
                yield return $"{what} has no <summary>, which its reference docs come from.";

            var problem = DefaultProblem(argument, what);
            if (problem != null)
                yield return $"The default doesn't fit: {problem}";

            var type = argument.Type.Kind == ArgumentKind.List ? argument.Type.ItemType : argument.Type;
            foreach (var smell in ArgumentSmells(what, type.Arguments))
                yield return smell;
        }
    }

    /// <summary>
    /// A default isn't written in any scenario, so nothing finds this until a step leaves the argument out and fails to bind.
    /// </summary>
    private static string DefaultProblem(ArgumentDescriptor argument, string what)
    {
        if (argument.Default == null || argument.Type.Kind is ArgumentKind.List or ArgumentKind.Object)
            return null;

        try
        {
            new WrittenValue(argument, argument.Type, WrittenValue.DefaultText(argument.Default), new SourceLocation(string.Empty, 0, 0), what).Read();
            return null;
        }
        catch (ScenarioException exception)
        {
            return exception.Problem;
        }
    }
}
