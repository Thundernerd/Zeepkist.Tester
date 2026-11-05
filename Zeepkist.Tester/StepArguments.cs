using System.Collections;
using System.Reflection;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What the harness does with a step's arguments before it runs the step.
/// </summary>
internal static class StepArguments
{
    /// <summary>
    /// Writes the <c>${name}</c>s of each <c>string</c> and <c>Text</c> argument, also in lists and nested arguments.
    /// This replaces the written form, so a step that runs again has to be bound again.
    /// </summary>
    /// <param name="owner">A step, or the value of a nested argument.</param>
    /// <param name="values">The running scenario's values.</param>
    /// <exception cref="StepFailedException">An argument uses <c>${name}</c> wrongly: see <see cref="IValues.Substitute"/>.</exception>
    public static void Substitute(object owner, IValues values)
    {
        foreach (var property in owner.GetType().GetProperties())
        {
            if (property.GetCustomAttribute<ArgAttribute>() == null)
                continue;

            var value = property.GetValue(owner);
            if (value != null)
                property.SetValue(owner, Substituted(value, values));
        }
    }

    private static object Substituted(object value, IValues values)
    {
        switch (value)
        {
            case string text:
                return values.Substitute(text);
            case Text { Value: not null } text:
                return new Text(values.Substitute(text.Value));
            case IList list:
                for (var i = 0; i < list.Count; i++)
                    list[i] = Substituted(list[i], values);

                return list;
            default:
                // Only a nested argument class has [Arg] properties to go through
                Substitute(value, values);
                return value;
        }
    }
}
