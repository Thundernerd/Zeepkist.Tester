using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The named values of one scenario, which end with it.
/// </summary>
internal sealed class ScenarioValues : IValues
{
    // In order: an escape, a name as Name<T>.Pattern allows it, and a ${ that's neither
    private static readonly Regex _substitutions = new Regex(@"\$\$\{|\$\{(?<name>[A-Za-z0-9_-]+)\}|\$\{");

    private readonly Dictionary<string, Defined> _values = new Dictionary<string, Defined>();

    public void Define<T>(string name, T value)
        where T : class
    {
        if (name == null)
            throw new StepFailedException("The step defines a named value, but its name was left out.");

        if (_values.TryGetValue(name, out var defined))
            throw new StepFailedException($"The name '{name}' is already defined, as a {defined.Kind}.");

        if (value == null)
            throw new StepFailedException($"The name '{name}' has to be defined with a value, not null.");

        _values.Add(name, new Defined(value, typeof(T)));
    }

    public T Get<T>(string name)
        where T : class
    {
        if (name == null)
            throw new StepFailedException("The step uses a named value, but its name was left out.");

        var defined = Find(name);
        if (defined.Value is T value)
            return value;

        throw new StepFailedException($"The name '{name}' is a {defined.Kind}, not a {KindOf(typeof(T))}.");
    }

    public string Substitute(string text)
    {
        return _substitutions.Replace(text, Write);
    }

    private string Write(Match match)
    {
        if (match.Value == "$${")
            return "${";

        var name = match.Groups["name"];
        if (!name.Success)
            throw new StepFailedException("A ${ has no name and } after it. Write $${ for a literal ${.");

        var defined = Find(name.Value);
        var member = defined.ValueType.GetCustomAttribute<ValueKindAttribute>()?.Substitute;
        if (member == null)
            throw new StepFailedException($"{match.Value} can't be written: a {defined.Kind} has no Substitute member.");

        return (string)defined.ValueType.GetProperty(member).GetValue(defined.Value)
            ?? throw new StepFailedException($"{match.Value} can't be written: the {defined.Kind}'s {member} is null.");
    }

    private Defined Find(string name)
    {
        if (_values.TryGetValue(name, out var defined))
            return defined;

        throw new StepFailedException($"The name '{name}' isn't defined by an earlier step.");
    }

    private static string KindOf(Type valueType)
    {
        return valueType.GetCustomAttribute<ValueKindAttribute>()?.Name ?? valueType.Name;
    }

    private sealed class Defined
    {
        public Defined(object value, Type valueType)
        {
            Value = value;
            ValueType = valueType;
        }

        public object Value { get; }

        public Type ValueType { get; }

        public string Kind => KindOf(ValueType);
    }
}
