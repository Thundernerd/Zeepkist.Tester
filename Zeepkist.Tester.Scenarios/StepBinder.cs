using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Makes a step from what the file wrote: each value is substituted, then converted to its argument's type. Binding
/// starts from the text again each time, so each pass of a loop gets a new step.
/// </summary>
internal sealed class StepBinder
{
    private static readonly Regex _seconds = new Regex(@"^(?<number>[0-9]+(?:\.[0-9]+)?)(?<unit>ms|s)?\z");

    private readonly IValues _values;

    private StepBinder(IValues values)
    {
        _values = values;
    }

    /// <exception cref="StepFailedException">A value uses a name wrongly or doesn't fit its argument. The message starts with <c>file:line:col</c>.</exception>
    public static Step Bind(StepNode node, Type stepType, IValues values)
    {
        var step = (Step)Activator.CreateInstance(stepType);
        new StepBinder(values).Fill(step, node.Step.FullName, node.Step.Arguments, node.Arguments);
        return step;
    }

    private void Fill(object owner, string ownerName, IReadOnlyList<ArgumentDescriptor> arguments, ObjectArgument written)
    {
        foreach (var argument in arguments)
        {
            var what = $"'{argument.Name}' of {ownerName}";
            var property = owner.GetType().GetProperty(argument.PropertyName);
            if (written.Values.TryGetValue(argument.Name, out var value))
            {
                if (!property.CanWrite)
                    throw Fail(value.Location, $"{what} can't be set: its property has no setter.");

                property.SetValue(owner, Convert(property.PropertyType, argument, argument.Type, value, what));
            }
            else if (argument.Default != null && property.CanWrite)
            {
                property.SetValue(owner, ConvertText(property.PropertyType, argument, argument.Type, DefaultText(argument.Default), written.Location, what));
            }
        }
    }

    private object Convert(Type type, ArgumentDescriptor argument, ArgumentType argumentType, ArgumentValue value, string what)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        switch (value)
        {
            case ListArgument list:
                if (list.Items.Count < argument.MinItems)
                    throw Fail(list.Location, $"{what} needs at least {argument.MinItems} item{(argument.MinItems == 1 ? string.Empty : "s")}, not {list.Items.Count}.");

                var items = (IList)Activator.CreateInstance(type);
                var itemType = type.GetGenericArguments()[0];
                foreach (var item in list.Items)
                    items.Add(Convert(itemType, argument, argumentType.ItemType, item, what));

                return items;
            case ObjectArgument nested:
                var instance = Activator.CreateInstance(type);
                Fill(instance, what, argumentType.Arguments, nested);
                return instance;
            default:
                var scalar = (ScalarArgument)value;
                return ConvertText(type, argument, argumentType, Substitute(scalar), scalar.Location, what);
        }
    }

    private string Substitute(ScalarArgument scalar)
    {
        try
        {
            return _values.Substitute(scalar.Text);
        }
        catch (StepFailedException exception)
        {
            throw Fail(scalar.Location, exception.Message);
        }
    }

    private static object ConvertText(Type type, ArgumentDescriptor argument, ArgumentType argumentType, string text, SourceLocation location, string what)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        var value = new WrittenValue(argument, text, location, what);
        switch (argumentType.Kind)
        {
            case ArgumentKind.Int:
                return value.ToInt();
            case ArgumentKind.Pixels:
                return new Pixels(value.ToInt());
            case ArgumentKind.Float:
                return value.ToFloat();
            case ArgumentKind.Seconds:
                return new Seconds(value.ToSeconds());
            case ArgumentKind.Bool:
                return value.ToBool();
            case ArgumentKind.Enum:
                return type.GetFields(BindingFlags.Public | BindingFlags.Static)[value.IndexIn(argumentType.EnumValues)].GetValue(null);
            case ArgumentKind.String:
                value.RequirePattern(argumentType);
                return text;
            default:
                // TextBlock, Keys, FileName, Name<T> and Ref<T> each hold the text
                value.RequirePattern(argumentType);
                return Activator.CreateInstance(type, text);
        }
    }

    private static string DefaultText(object value)
    {
        return value is bool flag ? (flag ? "true" : "false") : System.Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static StepFailedException Fail(SourceLocation location, string problem)
    {
        return new StepFailedException($"{location}: {problem}");
    }

    /// <summary>
    /// One value's text, with what its checks need to say what's wrong with it.
    /// </summary>
    private sealed class WrittenValue
    {
        private readonly ArgumentDescriptor _argument;
        private readonly string _text;
        private readonly SourceLocation _location;
        private readonly string _what;

        public WrittenValue(ArgumentDescriptor argument, string text, SourceLocation location, string what)
        {
            _argument = argument;
            _text = text;
            _location = location;
            _what = what;
        }

        public int ToInt()
        {
            if (!int.TryParse(_text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                throw Fail(_location, $"{_what} takes a whole number, not '{_text}'.");

            RequireRange(number);
            return number;
        }

        public float ToFloat()
        {
            if (!TryParseFloat(_text, out var number))
                throw Fail(_location, $"{_what} takes a number, not '{_text}'.");

            RequireRange(number);
            return (float)number;
        }

        public float ToSeconds()
        {
            var match = _seconds.Match(_text);
            if (!match.Success || !TryParseFloat(match.Groups["number"].Value, out var number))
                throw Fail(_location, $"{_what} takes a length of time such as 2, 0.5 or 500ms, not '{_text}'.");

            var seconds = match.Groups["unit"].Value == "ms" ? number / 1000 : number;
            RequireRange(seconds);
            return (float)seconds;
        }

        public bool ToBool()
        {
            return _text == "true" || (_text == "false" ? false : throw Fail(_location, $"{_what} takes true or false, not '{_text}'."));
        }

        public int IndexIn(IReadOnlyList<string> enumValues)
        {
            var index = enumValues.ToList().IndexOf(_text);
            if (index < 0)
                throw Fail(_location, $"{_what} takes {Choices(enumValues)}, not '{_text}'.");

            return index;
        }

        /// <summary>
        /// A list's items have their pattern on their own type, unless the argument gives one.
        /// </summary>
        public void RequirePattern(ArgumentType argumentType)
        {
            var pattern = _argument.Pattern ?? argumentType.Pattern;
            if (pattern != null && !Regex.IsMatch(_text, ToEndOfText(pattern)))
                throw Fail(_location, $"{_what} has to match {pattern}, not '{_text}'.");
        }

        private void RequireRange(double number)
        {
            if (number < _argument.Minimum || number > _argument.Maximum)
                throw Fail(_location, $"{_what} has to be between {_argument.Minimum.Value.ToString(CultureInfo.InvariantCulture)} and {_argument.Maximum.Value.ToString(CultureInfo.InvariantCulture)}, not '{_text}'.");
        }

        /// <summary>
        /// Reads a float as a double, so a range compares what was written and not the nearest float to it.
        /// </summary>
        private static bool TryParseFloat(string text, out double number)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !float.IsNaN((float)number) && !float.IsInfinity((float)number);
        }

        /// <summary>
        /// A pattern is written for the schema too, where <c>$</c> is the end of the text. Here it also matches before a line break at the end, as a <c>|</c> block has.
        /// </summary>
        private static string ToEndOfText(string pattern)
        {
            return pattern.EndsWith("$", StringComparison.Ordinal) && !pattern.EndsWith(@"\$", StringComparison.Ordinal)
                ? pattern.Substring(0, pattern.Length - 1) + @"\z"
                : pattern;
        }

        private static string Choices(IReadOnlyList<string> values)
        {
            return values.Count < 2 ? string.Join(string.Empty, values) : $"{string.Join(", ", values.Take(values.Count - 1))} or {values[values.Count - 1]}";
        }
    }
}
