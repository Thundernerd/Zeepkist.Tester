using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Makes a step from what the file wrote: each value is substituted, then converted to its argument's type, which
/// <see cref="WrittenValue"/> checks. Binding starts from the text again each time, so each pass of a loop gets a new step.
/// </summary>
internal sealed class StepBinder
{
    private readonly IValues _values;

    private StepBinder(IValues values)
    {
        _values = values;
    }

    /// <exception cref="StepFailedException">A value uses a name wrongly or doesn't fit its argument. The message starts with <c>file:line:col</c>.</exception>
    public static Step Bind(StepNode node, Type stepType, IValues values)
    {
        var step = (Step)Activator.CreateInstance(stepType);
        try
        {
            new StepBinder(values).Fill(step, node.Step.FullName, node.Step.Arguments, node.Arguments);
        }
        catch (ScenarioException exception)
        {
            // Binding happens as the step runs, where a mistake in the file fails the step
            throw new StepFailedException(exception.Message);
        }

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
                    throw new ScenarioException(value.Location, $"{what} can't be set: its property has no setter.");

                property.SetValue(owner, Convert(property.PropertyType, argument, argument.Type, value, what));
            }
            else if (argument.Default != null && property.CanWrite)
            {
                property.SetValue(owner, ConvertText(property.PropertyType, argument, argument.Type, WrittenValue.DefaultText(argument.Default), written.Location, what));
            }
        }
    }

    private object Convert(Type type, ArgumentDescriptor argument, ArgumentType argumentType, ArgumentValue value, string what)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return value.Match(
            list =>
            {
                var items = (IList)Activator.CreateInstance(type);
                var itemType = type.GetGenericArguments()[0];
                foreach (var item in list.Items)
                    items.Add(Convert(itemType, argument, argumentType.ItemType, item, what));

                return items;
            },
            nested =>
            {
                var instance = Activator.CreateInstance(type);
                Fill(instance, what, argumentType.Arguments, nested);
                return instance;
            },
            scalar => ConvertText(type, argument, argumentType, Substitute(scalar), scalar.Location, what));
    }

    private string Substitute(ScalarArgument scalar)
    {
        try
        {
            return _values.Substitute(scalar.Text);
        }
        catch (StepFailedException exception)
        {
            throw new ScenarioException(scalar.Location, exception.Message);
        }
    }

    private static object ConvertText(Type type, ArgumentDescriptor argument, ArgumentType argumentType, string text, SourceLocation location, string what)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        var written = new WrittenValue(argument, argumentType, text, location, what);
        switch (argumentType.Kind)
        {
            case ArgumentKind.Int:
                return written.ReadInt();
            case ArgumentKind.Float:
                return written.ReadFloat();
            case ArgumentKind.Bool:
                return written.ReadBool();
            case ArgumentKind.Pixels:
                return new Pixels(written.ReadInt());
            case ArgumentKind.Seconds:
                return new Seconds(written.ReadSeconds());
            case ArgumentKind.Enum:
                return type.GetFields(BindingFlags.Public | BindingFlags.Static)[written.ReadEnumIndex()].GetValue(null);
            case ArgumentKind.String:
                return written.ReadText();
            default:
                // TextBlock, Keys, FileName, Name<T> and Ref<T> each hold the text
                return Activator.CreateInstance(type, written.ReadText());
        }
    }
}
