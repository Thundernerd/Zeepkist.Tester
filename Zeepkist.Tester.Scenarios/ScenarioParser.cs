using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TNRD.Zeepkist.Tester.Sdk;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Reads a scenario file against the registry's steps. Every scalar stays the text it was written as, for
/// <see cref="StepBinder"/> to convert.
/// </summary>
internal sealed class ScenarioParser
{
    private const string SingleValueOnly = " A ${name} only stands in for a single value, never a whole list or map.";

    private static readonly Regex _namePattern = new Regex(Name<object>.Pattern.Replace("$", @"\z"));

    private readonly string _file;
    private readonly Dictionary<string, StepDescriptor> _steps;
    private readonly List<YamlSequenceNode> _bodiesRead = [];
    private readonly Dictionary<string, IReadOnlyList<string>> _loopValues = [];

    /// <summary>
    /// Where problems go when reading goes on after them. Null when the first one ends the reading.
    /// </summary>
    private readonly List<ScenarioException> _problems;

    private ScenarioParser(string file, IEnumerable<StepDescriptor> steps, List<ScenarioException> problems)
    {
        _file = file;
        _steps = steps.ToDictionary(step => step.FullName);
        _problems = problems;
    }

    /// <exception cref="ScenarioException">The file can't be read as a scenario. This is the first problem in it.</exception>
    public static Scenario Parse(string file, TextReader reader, IEnumerable<StepDescriptor> steps)
    {
        return new ScenarioParser(file, steps, null).ReadScenario(ScenarioYaml.Load(file, reader));
    }

    /// <summary>
    /// Reads a scenario file the way <see cref="Parse"/> does, but goes on after each problem it can, such as a value
    /// that doesn't fit its argument, so <c>ztest check</c> can report them all at once. A file that isn't valid YAML
    /// has only that problem.
    /// </summary>
    public static ScenarioReading ParseAll(string file, TextReader reader, IEnumerable<StepDescriptor> steps)
    {
        var problems = new List<ScenarioException>();
        Scenario scenario;
        try
        {
            scenario = new ScenarioParser(file, steps, problems).ReadScenario(ScenarioYaml.Load(file, reader));
        }
        catch (ScenarioException exception)
        {
            problems.Add(exception);
            scenario = null;
        }

        return new ScenarioReading(problems.Count == 0 ? scenario : null, problems);
    }

    private Scenario ReadScenario(YamlStream stream)
    {
        if (stream.Documents.Count == 0)
            throw new ScenarioException(new SourceLocation(_file, 1, 1), "The file has no scenario in it.");

        if (stream.Documents.Count > 1)
            throw Fail(stream.Documents[1].RootNode, "A scenario is one YAML document, but this is another.");

        var root = stream.Documents[0].RootNode;
        if (root is not YamlMappingNode header)
            throw Fail(root, $"A scenario is a map with a steps: key, not {Describe(root)}.");

        var entries = ReadKeys(header, "A scenario", "description", "use", "start", "input", "steps");
        if (!entries.TryGetValue("steps", out var steps))
            Report(Fail(header, "A scenario needs steps:."));

        // Nothing in the header depends on another part of it, so each is read on its own
        var description = entries.TryGetValue("description", out var written) ? Attempt(() => ReadScalar(written, "description")) : null;
        var start = entries.TryGetValue("start", out var startWritten) ? Attempt(() => ReadStart(startWritten)) : ScenarioStart.Default;
        var input = entries.TryGetValue("input", out var inputWritten) ? Attempt(() => ReadInput(inputWritten)) : InputRoute.InProcess;
        var use = entries.TryGetValue("use", out var useWritten) ? Attempt(() => ReadUse(useWritten)) ?? [] : [];
        if (steps == null)
            return null;

        return new Scenario(_file, description, use, start, input, ReadBody(steps, use));
    }

    private List<string> ReadUse(YamlNode written)
    {
        if (written is not YamlSequenceNode list)
            throw Fail(written, "use: takes a list of namespaces, such as [zls].");

        return list.Select(item => Attempt(() => ReadNamespace(item))).Where(space => space != null).Distinct().ToList();
    }

    private string ReadNamespace(YamlNode item)
    {
        var space = ReadScalar(item, "A namespace of use:");
        if (space.Length == 0 || !_steps.Values.Any(step => step.Namespace == space))
            throw Fail(item, $"use: lists '{space}', but no step has that namespace.");

        return space;
    }

    private ScenarioStart ReadStart(YamlNode written)
    {
        string name;
        YamlNode value = null;
        if (written is YamlMappingNode map && map.Children.Count > 0)
        {
            if (map.Children.Count > 1)
                throw Fail(map.Children.Keys.ElementAt(1), "start: takes one state, but this is another.");

            name = ReadKey(map.Children.Keys.First());
            value = map.Children.Values.First();
        }
        else
        {
            name = ReadScalar(written, "start:");
        }

        var state = StartState.All.FirstOrDefault(known => known.Name == name);
        if (state == null)
        {
            var known = StartState.All.Select(each => each.Name).ToList();
            throw Fail(written, $"start: takes {string.Join(", ", known.Take(known.Count - 1))} or {known.Last()}, not '{name}'.");
        }

        var arguments = new Dictionary<string, string>();
        if (value != null)
        {
            var names = state.Argument == null ? [] : new[] { state.Argument };
            foreach (var entry in ReadKeys(value, $"start: {name}", names, "argument"))
                arguments.Add(entry.Key, ReadLiteral(entry.Value, $"'{entry.Key}' of start: {name}"));
        }

        if (state.NeedsArgument && !arguments.ContainsKey(state.Argument))
            throw Fail(written, $"start: {name} needs the argument '{state.Argument}'.");

        return new ScenarioStart(state, arguments, Location(written));
    }

    private InputRoute ReadInput(YamlNode written)
    {
        var input = ReadScalar(written, "input:");
        return input switch
        {
            "in-process" => InputRoute.InProcess,
            "os" => InputRoute.Os,
            _ => throw Fail(written, $"input: takes in-process or os, not '{input}'."),
        };
    }

    /// <param name="steps">The value of a steps: key.</param>
    /// <param name="use">The namespaces whose steps can be written unprefixed.</param>
    private List<ScenarioNode> ReadBody(YamlNode steps, IReadOnlyList<string> use)
    {
        if (steps is not YamlSequenceNode list)
            throw Fail(steps, $"steps: takes a list of steps, not {Describe(steps)}.");

        if (list.Children.Count == 0)
            throw Fail(steps, "steps: has no steps in it.");

        // An alias can make a body its own child, or double the work at each level, which would take the game down
        if (_bodiesRead.Any(body => ReferenceEquals(body, list)))
            throw Fail(steps, "steps: is used a second time through an alias. Use run: to share steps.");

        _bodiesRead.Add(list);
        return list.Select(item => Attempt(() => ReadNode(item, use))).Where(node => node != null).ToList();
    }

    private ScenarioNode ReadNode(YamlNode item, IReadOnlyList<string> use)
    {
        if (item is YamlSequenceNode)
            throw Fail(item, "A step is a name or a map with its name as the key, not a list.");

        if (item is YamlScalarNode bare)
        {
            var named = Resolve(bare, use);
            return Checked(new StepNode(Location(item), named, ReadNoArguments(named, item)));
        }

        var map = (YamlMappingNode)item;
        if (map.Children.Count == 0)
            throw Fail(item, "A step is a name or a map with its name as the key, but this map is empty.");

        var written = map.Children.Keys.Select(ReadKey).ToList();
        if (written.Contains(ControlNodes.For))
            return ReadFor(map, use);

        if (written.Contains(ControlNodes.Repeat))
            return ReadRepeat(map, use);

        if (written.Contains(ControlNodes.Run))
            return ReadRun(map);

        if (map.Children.Count != 1)
            throw Fail(map.Children.Keys.ElementAt(1), "A step has one key, but this is another. Is it indented too far, or missing its - ?");

        var entry = map.Children.First();
        var step = Resolve(entry.Key, use);
        return Checked(new StepNode(Location(item), step, ReadObject(step.FullName, step.Arguments, entry.Value)));
    }

    private StepNode Checked(StepNode node)
    {
        ValueChecks.Check(node, _loopValues, Report);
        return node;
    }

    private ForNode ReadFor(YamlMappingNode map, IReadOnlyList<string> use)
    {
        var entries = ReadKeys(map, "for:", ControlNodes.For, "steps");
        if (entries[ControlNodes.For] is not YamlMappingNode variables || variables.Children.Count == 0)
            throw Fail(entries[ControlNodes.For], "for: takes a variable and its values, such as { h: [64, 120] }.");

        if (variables.Children.Count > 1)
            throw Fail(variables.Children.Keys.ElementAt(1), "A for has one variable, but this is another. Nest two fors for every combination.");

        var variable = variables.Children.First();
        var name = ReadName(variable.Key, "a variable's name");
        if (variable.Value is not YamlSequenceNode values)
            throw Fail(variable.Value, $"for: takes a list of values for '{name}'.");

        if (values.Children.Count == 0)
            throw Fail(values, $"for: has no values for '{name}'.");

        var literals = values.Select(value => ReadLiteral(value, "A value of for:")).ToList();
        var body = RequireSteps(map, entries, "for:");

        // A body's own for of this name is an error in itself, which ztest check reports. Here the inner one wins, as it would until then
        _loopValues.TryGetValue(name, out var outer);
        _loopValues[name] = literals;
        List<ScenarioNode> steps;
        try
        {
            steps = ReadBody(body, use);
        }
        finally
        {
            // Reading goes on after a body that can't be read, and the steps after it can't see its variable
            if (outer == null)
                _loopValues.Remove(name);
            else
                _loopValues[name] = outer;
        }

        return new ForNode(Location(map), name, literals, steps);
    }

    private RepeatNode ReadRepeat(YamlMappingNode map, IReadOnlyList<string> use)
    {
        var entries = ReadKeys(map, "repeat:", ControlNodes.Repeat, "steps");
        var written = ReadScalar(entries[ControlNodes.Repeat], "repeat:");
        if (!int.TryParse(written, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
            throw Fail(entries[ControlNodes.Repeat], $"repeat: takes a whole number of at least 1, not '{written}'.");

        return new RepeatNode(Location(map), count, ReadBody(RequireSteps(map, entries, "repeat:"), use));
    }

    private RunNode ReadRun(YamlMappingNode map)
    {
        var entries = ReadKeys(map, "run:", ControlNodes.Run, "with");
        var file = ReadLiteral(entries[ControlNodes.Run], "run:");
        var with = new Dictionary<string, ScalarArgument>();
        if (entries.TryGetValue("with", out var written))
        {
            if (written is not YamlMappingNode values)
                throw Fail(written, "with: takes a map of names and values.");

            foreach (var entry in values.Children)
            {
                var name = ReadName(entry.Key, "a name");
                if (with.ContainsKey(name))
                    throw Fail(entry.Key, $"with: has the name '{name}' twice.");

                var value = new ScalarArgument(Location(entry.Value), ReadScalar(entry.Value, $"'{name}' of with:"));
                ValueChecks.RequireWrittenProperly(value);
                with.Add(name, value);
            }
        }

        return new RunNode(Location(map), file, with);
    }

    private YamlNode RequireSteps(YamlMappingNode map, Dictionary<string, YamlNode> entries, string node)
    {
        return entries.TryGetValue("steps", out var steps) ? steps : throw Fail(map, $"{node} needs steps:.");
    }

    /// <summary>
    /// Finds the step a name means: a prefixed name is that step, and any other is the tester's or a <c>use:</c>d namespace's.
    /// </summary>
    private StepDescriptor Resolve(YamlNode node, IReadOnlyList<string> use)
    {
        var written = ReadKey(node);
        if (written.Contains('.'))
            return _steps.TryGetValue(written, out var prefixed) ? prefixed : throw Fail(node, $"There's no step '{written}'.");

        var used = use.Where(space => _steps.ContainsKey($"{space}.{written}")).Select(space => _steps[$"{space}.{written}"]).ToList();
        if (_steps.TryGetValue(written, out var core))
        {
            if (used.Count == 0)
                return core;

            var mod = used[0];
            throw Fail(node, $"'{written}' could be the tester's own step or {mod.FullName}. Write {mod.FullName} for the mod's, or take {mod.Namespace} out of use: for the tester's.");
        }

        if (used.Count > 1)
            throw Fail(node, $"'{written}' could be {string.Join(" or ", used.Select(step => step.FullName))}. Write the one you mean with its prefix.");

        if (used.Count == 1)
            return used[0];

        var elsewhere = _steps.Values.Where(step => step.Name == written).Select(step => step.FullName).ToList();
        var hint = elsewhere.Count == 0 ? string.Empty : $" Write {string.Join(" or ", elsewhere)}, or list the namespace under use:.";
        throw Fail(node, $"There's no step '{written}'.{hint}");
    }

    private ObjectArgument ReadNoArguments(StepDescriptor step, YamlNode node)
    {
        RequireArguments(step.FullName, step.Arguments, [], node);
        var values = new Dictionary<string, ArgumentValue>();
        return new ObjectArgument(Location(node), values);
    }

    /// <summary>
    /// Reads the value of a step or a nested class, which <paramref name="owner"/> names in errors, given as a map of its arguments or as its primary argument alone.
    /// </summary>
    private ObjectArgument ReadObject(string owner, IReadOnlyList<ArgumentDescriptor> arguments, YamlNode value)
    {
        var values = new Dictionary<string, ArgumentValue>();
        var given = new List<string>();
        if (value is YamlMappingNode map && arguments.Count > 0 && !IsPrimaryItem(arguments, map))
        {
            var names = arguments.Select(argument => argument.Name).ToList();
            foreach (var entry in ReadKeys(map, owner, names, "argument"))
            {
                // A value that can't be read is still given, so it isn't missing as well
                given.Add(entry.Key);
                var argument = arguments.First(other => other.Name == entry.Key);
                var read = Attempt(() => ReadValue(argument.Type, entry.Value, $"'{entry.Key}' of {owner}"));
                if (read != null)
                    values.Add(entry.Key, read);
            }
        }
        else
        {
            var primary = arguments.FirstOrDefault(argument => argument.IsPrimary);
            if (primary == null)
            {
                throw Fail(value, arguments.Count == 0
                    ? $"{owner} takes no arguments, so write it as a bare string."
                    : $"{owner} can't take a plain value. Write a map of its arguments: {string.Join(", ", arguments.Select(argument => argument.Name))}.{HintFor(value)}");
            }

            given.Add(primary.Name);
            values.Add(primary.Name, ReadPrimary(primary.Type, value, $"'{primary.Name}' of {owner}"));
        }

        RequireArguments(owner, arguments, given, value);
        return new ObjectArgument(Location(value), values);
    }

    /// <summary>
    /// Whether a map is one item of the primary argument instead of the owner's own arguments, as in
    /// <c>- drag-window-edge: { by: 50, over: 3 }</c>. A key of the owner's own makes it the owner's.
    /// </summary>
    private static bool IsPrimaryItem(IReadOnlyList<ArgumentDescriptor> arguments, YamlMappingNode map)
    {
        var primary = arguments.FirstOrDefault(argument => argument.IsPrimary)?.Type;
        if (primary?.Kind == ArgumentKind.List)
            primary = primary.ItemType;

        return primary?.Kind == ArgumentKind.Object
            && map.Children.Count > 0
            && !map.Children.Keys.OfType<YamlScalarNode>().Any(key => arguments.Any(argument => argument.Name == key.Value));
    }

    /// <param name="given">The names of the arguments the file wrote.</param>
    private void RequireArguments(string owner, IReadOnlyList<ArgumentDescriptor> arguments, IReadOnlyCollection<string> given, YamlNode node)
    {
        var missing = arguments.FirstOrDefault(argument => argument.IsRequired && !given.Contains(argument.Name));
        if (missing != null)
            throw Fail(node, $"{owner} needs the argument '{missing.Name}'.");
    }

    /// <summary>
    /// A primary list also takes one item.
    /// </summary>
    private ArgumentValue ReadPrimary(ArgumentType type, YamlNode value, string what)
    {
        if (type.Kind == ArgumentKind.List && value is not YamlSequenceNode)
            return new ListArgument(Location(value), [ReadValue(type.ItemType, value, what)]);

        return ReadValue(type, value, what);
    }

    private ArgumentValue ReadValue(ArgumentType type, YamlNode value, string what)
    {
        switch (type.Kind)
        {
            case ArgumentKind.List:
                if (value is not YamlSequenceNode list)
                    throw Fail(value, $"{what} takes a list.{HintFor(value)}");

                return new ListArgument(Location(value), list.Select(item => ReadValue(type.ItemType, item, what)).ToList());
            case ArgumentKind.Object:
                return ReadObject(what, type.Arguments, value);
            default:
                return new ScalarArgument(Location(value), ReadScalar(value, what));
        }
    }

    private static string HintFor(YamlNode value)
    {
        return value is YamlScalarNode { Value: not null } scalar && scalar.Value.Contains("${") ? SingleValueOnly : string.Empty;
    }

    /// <summary>
    /// The value of a control node or of <c>start:</c>, which has no expressions, and no step has run yet to define a name.
    /// </summary>
    private string ReadLiteral(YamlNode node, string what)
    {
        var text = ReadScalar(node, what);
        if (text.Contains("${"))
            throw Fail(node, $"{what} has to be a literal, not '{text}'.");

        return text;
    }

    private string ReadScalar(YamlNode node, string what)
    {
        if (node is not YamlScalarNode scalar)
            throw Fail(node, $"{what} takes a single value, not {Describe(node)}.");

        // Only quotes can write an empty text, so this is a key with nothing after it
        if (string.IsNullOrEmpty(scalar.Value) && scalar.Style is ScalarStyle.Plain or ScalarStyle.Any)
            throw Fail(node, $"{what} has no value.");

        return scalar.Value ?? string.Empty;
    }

    private string ReadName(YamlNode node, string what)
    {
        var name = ReadKey(node);
        if (!_namePattern.IsMatch(name))
            throw Fail(node, $"'{name}' can't be {what}: it has to match {Name<object>.Pattern}.");

        return name;
    }

    private Dictionary<string, YamlNode> ReadKeys(YamlNode node, string owner, params string[] allowed)
    {
        return ReadKeys(node, owner, allowed, "key");
    }

    /// <summary>
    /// A map's entries by key, where a key that isn't <paramref name="allowed"/> is an error.
    /// </summary>
    private Dictionary<string, YamlNode> ReadKeys(YamlNode node, string owner, IReadOnlyCollection<string> allowed, string keyKind)
    {
        if (node is not YamlMappingNode map)
            throw Fail(node, $"{owner} takes a map, not {Describe(node)}.");

        var entries = new Dictionary<string, YamlNode>();
        foreach (var entry in map.Children)
        {
            var key = ReadKey(entry.Key);
            if (!allowed.Contains(key))
            {
                Report(Fail(entry.Key, $"{owner} has no {keyKind} '{key}'. {(allowed.Count == 0 ? "It takes none." : $"It takes: {string.Join(", ", allowed)}.")}"));
                continue;
            }

            // YamlDotNet's own check for this tells a key with a tag from one without
            if (entries.ContainsKey(key))
            {
                Report(Fail(entry.Key, $"{owner} has the {keyKind} '{key}' twice."));
                continue;
            }

            entries.Add(key, entry.Value);
        }

        return entries;
    }

    private string ReadKey(YamlNode node)
    {
        return node is YamlScalarNode { Value: not null } key ? key.Value : throw Fail(node, $"A key has to be a single value, not {Describe(node)}.");
    }

    private static string Describe(YamlNode node)
    {
        return node is YamlSequenceNode ? "a list" : node is YamlMappingNode ? "a map" : "a single value";
    }

    /// <summary>
    /// Reads a part of the file that the rest doesn't depend on. When reading goes on after problems, a problem in it is
    /// kept and the part is left out, as the default.
    /// </summary>
    private T Attempt<T>(Func<T> read)
    {
        if (_problems == null)
            return read();

        try
        {
            return read();
        }
        catch (ScenarioException exception)
        {
            Report(exception);
            return default;
        }
    }

    /// <summary>
    /// Keeps a problem when reading goes on after problems, and otherwise ends the reading with it.
    /// </summary>
    private void Report(ScenarioException problem)
    {
        if (_problems == null)
            throw problem;

        _problems.Add(problem);
    }

    private ScenarioException Fail(YamlNode node, string problem)
    {
        return new ScenarioException(Location(node), problem);
    }

    private SourceLocation Location(YamlNode node)
    {
        return new SourceLocation(_file, node.Start.Line, node.Start.Column);
    }
}
