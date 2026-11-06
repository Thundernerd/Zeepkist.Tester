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
        switch (argumentType.Kind)
        {
            case ArgumentKind.Int:
                return ParseInt(argument, text, location, what);
            case ArgumentKind.Pixels:
                return new Pixels(ParseInt(argument, text, location, what));
            case ArgumentKind.Float:
                if (!TryParseFloat(text, out var number))
                    throw Fail(location, $"{what} takes a number, not '{text}'.");

                RequireRange(number, argument, text, location, what);
                return (float)number;
            case ArgumentKind.Seconds:
                return new Seconds(ParseSeconds(argument, text, location, what));
            case ArgumentKind.Bool:
                return text == "true" || (text == "false" ? false : throw Fail(location, $"{what} takes true or false, not '{text}'."));
            case ArgumentKind.Enum:
                var index = argumentType.EnumValues.ToList().IndexOf(text);
                if (index < 0)
                    throw Fail(location, $"{what} takes {Choices(argumentType.EnumValues)}, not '{text}'.");

                return type.GetFields(BindingFlags.Public | BindingFlags.Static)[index].GetValue(null);
            case ArgumentKind.String:
                RequirePattern(argument, argumentType, text, location, what);
                return text;
            default:
                // TextBlock, Keys, FileName, Name<T> and Ref<T> each hold the text
                RequirePattern(argument, argumentType, text, location, what);
                return Activator.CreateInstance(type, text);
        }
    }

    private static int ParseInt(ArgumentDescriptor argument, string text, SourceLocation location, string what)
    {
        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            throw Fail(location, $"{what} takes a whole number, not '{text}'.");

        RequireRange(number, argument, text, location, what);
        return number;
    }

    private static float ParseSeconds(ArgumentDescriptor argument, string text, SourceLocation location, string what)
    {
        var match = _seconds.Match(text);
        if (!match.Success || !TryParseFloat(match.Groups["number"].Value, out var number))
            throw Fail(location, $"{what} takes a length of time such as 2, 0.5 or 500ms, not '{text}'.");

        var seconds = match.Groups["unit"].Value == "ms" ? number / 1000 : number;
        RequireRange(seconds, argument, text, location, what);
        return (float)seconds;
    }

    /// <summary>
    /// Reads a float as a double, so a range compares what was written and not the nearest float to it.
    /// </summary>
    private static bool TryParseFloat(string text, out double number)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !float.IsNaN((float)number) && !float.IsInfinity((float)number);
    }

    private static void RequireRange(double number, ArgumentDescriptor argument, string text, SourceLocation location, string what)
    {
        if (number < argument.Minimum || number > argument.Maximum)
            throw Fail(location, $"{what} has to be between {argument.Minimum.Value.ToString(CultureInfo.InvariantCulture)} and {argument.Maximum.Value.ToString(CultureInfo.InvariantCulture)}, not '{text}'.");
    }

    /// <summary>
    /// A list's items have their pattern on their own type, unless the argument gives one.
    /// </summary>
    private static void RequirePattern(ArgumentDescriptor argument, ArgumentType argumentType, string text, SourceLocation location, string what)
    {
        var pattern = argument.Pattern ?? argumentType.Pattern;
        if (pattern != null && !Regex.IsMatch(text, ToEndOfText(pattern)))
            throw Fail(location, $"{what} has to match {pattern}, not '{text}'.");
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

    private static string DefaultText(object value)
    {
        return value is bool flag ? (flag ? "true" : "false") : System.Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static StepFailedException Fail(SourceLocation location, string problem)
    {
        return new StepFailedException($"{location}: {problem}");
    }
}
