using System;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// An argument's value as the file wrote it, which is converted to the argument's type each time a step is bound.
/// </summary>
internal abstract class ArgumentValue
{
    protected ArgumentValue(SourceLocation location)
    {
        Location = location;
    }

    public SourceLocation Location { get; }

    /// <summary>
    /// Calls the function for what this value is, so each walk over values handles all three.
    /// </summary>
    public abstract TResult Match<TResult>(Func<ListArgument, TResult> list, Func<ObjectArgument, TResult> nested, Func<ScalarArgument, TResult> scalar);

    /// <inheritdoc cref="Match{TResult}"/>
    public void Match(Action<ListArgument> list, Action<ObjectArgument> nested, Action<ScalarArgument> scalar)
    {
        Match<object>(value => Do(list, value), value => Do(nested, value), value => Do(scalar, value));
    }

    private static object Do<TValue>(Action<TValue> action, TValue value)
    {
        action(value);
        return null;
    }
}
