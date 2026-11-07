using System;
using System.Collections.Generic;
using System.Reflection;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The named values of one scope: a scenario, a pass of a <c>for</c> or <c>repeat</c> body, or a <c>run:</c> file. What a
/// scope defines ends with it. It can read what's around it, and defining one of those names again is an error, so nothing
/// gets silently hidden.
/// </summary>
internal sealed class ScenarioValues : IValues
{
    private readonly Dictionary<string, Defined> _values = new Dictionary<string, Defined>();
    private readonly ScenarioValues _outer;

    public ScenarioValues()
    {
    }

    private ScenarioValues(ScenarioValues outer)
    {
        _outer = outer;
    }

    /// <summary>
    /// A scope for a pass of a body, which reads this one.
    /// </summary>
    public ScenarioValues NewScope()
    {
        return new ScenarioValues(this);
    }

    public void Define<T>(string name, T value)
        where T : class
    {
        if (name == null)
            throw new StepFailedException("The step defines a named value, but its name was left out.");

        var defined = FindOrNull(name);
        if (defined != null)
            throw new StepFailedException(ScenarioMessages.AlreadyDefined(name, defined.Kind));

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

        throw new StepFailedException(ScenarioMessages.WrongKind(name, defined.Kind, KindOf(typeof(T))));
    }

    public string Substitute(string text)
    {
        return Substitution.Replace(text, WrittenText);
    }

    private string WrittenText(string name)
    {
        var defined = Find(name);
        if (defined.SubstituteMember == null)
            throw new StepFailedException(ScenarioMessages.NoSubstituteMember(name, defined.Kind));

        return defined.Substitute
            ?? throw new StepFailedException($"${{{name}}} can't be written: the {defined.Kind}'s {defined.SubstituteMember} is null.");
    }

    private Defined Find(string name)
    {
        return FindOrNull(name) ?? throw new StepFailedException(ScenarioMessages.NotDefined(name));
    }

    private Defined FindOrNull(string name)
    {
        return _values.TryGetValue(name, out var defined) ? defined : _outer?.FindOrNull(name);
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

        public string SubstituteMember => ValueType.GetCustomAttribute<ValueKindAttribute>()?.Substitute;

        public string Substitute => (string)ValueType.GetProperty(SubstituteMember).GetValue(Value);
    }
}
