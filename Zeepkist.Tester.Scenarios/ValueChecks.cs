using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Checks what a scenario wrote when it loads, so a mistake fails there and not minutes into a run: every value that's
/// known by then, which is a literal or a value made only of <c>for</c> variables, tried with each of their values. A value
/// from a step's <c>as:</c> or from <c>with:</c> is only known when the step binds.
/// </summary>
internal sealed class ValueChecks
{
    // Nested fors multiply, and a value that uses them all shouldn't keep a scenario from loading
    private const int MostCombinations = 1000;

    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _loopValues;
    private readonly Action<ScenarioException> _report;

    private ValueChecks(IReadOnlyDictionary<string, IReadOnlyList<string>> loopValues, Action<ScenarioException> report)
    {
        _loopValues = loopValues;
        _report = report;
    }

    /// <param name="node">The step to check.</param>
    /// <param name="loopValues">The values of each <c>for</c> variable around the step.</param>
    /// <param name="report">Takes each value that doesn't fit its argument, in the order they're written. It may throw to stop at the first.</param>
    public static void Check(StepNode node, IReadOnlyDictionary<string, IReadOnlyList<string>> loopValues, Action<ScenarioException> report)
    {
        new ValueChecks(loopValues, report).CheckArguments(node.Step.FullName, node.Step.Arguments, node.Arguments);
    }

    /// <exception cref="ScenarioException">A <c>${</c> has no name and <c>}</c> after it.</exception>
    public static void RequireWrittenProperly(ScalarArgument scalar)
    {
        NamesIn(scalar);
    }

    private void CheckArguments(string owner, IReadOnlyList<ArgumentDescriptor> arguments, ObjectArgument written)
    {
        foreach (var argument in arguments)
        {
            if (written.Values.TryGetValue(argument.Name, out var value))
                CheckValue(argument, argument.Type, value, $"'{argument.Name}' of {owner}");
        }
    }

    private void CheckValue(ArgumentDescriptor argument, ArgumentType type, ArgumentValue value, string what)
    {
        value.Match(
            list =>
            {
                if (list.Items.Count < argument.MinItems)
                    _report(new ScenarioException(list.Location, $"{what} needs at least {argument.MinItems} item{(argument.MinItems == 1 ? string.Empty : "s")}, not {list.Items.Count}."));

                foreach (var item in list.Items)
                    CheckValue(argument, type.ItemType, item, what);
            },
            nested => CheckArguments(what, type.Arguments, nested),
            scalar => CheckScalar(argument, type, scalar, what));
    }

    /// <summary>
    /// Reports the first problem with a scalar, as one value written wrongly would be the same mistake for each value of a <c>for</c>.
    /// </summary>
    private void CheckScalar(ArgumentDescriptor argument, ArgumentType type, ScalarArgument scalar, string what)
    {
        try
        {
            var names = NamesIn(scalar).Distinct().ToList();
            if (!names.All(_loopValues.ContainsKey))
                return;

            foreach (var values in Combinations(names).Take(MostCombinations))
                new WrittenValue(argument, type, Substitution.Replace(scalar.Text, name => values[name]), scalar.Location, what).Check();
        }
        catch (ScenarioException exception)
        {
            _report(exception);
        }
    }

    /// <summary>
    /// Each way to choose one value for each of the names, as they're asked for. With no names, that's the one way that chooses nothing.
    /// </summary>
    private IEnumerable<Dictionary<string, string>> Combinations(List<string> names)
    {
        IEnumerable<Dictionary<string, string>> combinations = [new Dictionary<string, string>()];
        foreach (var name in names)
            combinations = combinations.SelectMany(chosen => _loopValues[name].Select(value => new Dictionary<string, string>(chosen) { [name] = value }));

        return combinations;
    }

    private static IReadOnlyList<string> NamesIn(ScalarArgument scalar)
    {
        try
        {
            return Substitution.NamesIn(scalar.Text);
        }
        catch (StepFailedException exception)
        {
            throw new ScenarioException(scalar.Location, exception.Message);
        }
    }
}
