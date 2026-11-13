using System;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A single value, as its YAML text.
/// </summary>
internal sealed class ScalarArgument : ArgumentValue
{
    public ScalarArgument(SourceLocation location, string text)
        : base(location)
    {
        Text = text;
    }

    public string Text { get; }

    public override TResult Match<TResult>(Func<ListArgument, TResult> list, Func<ObjectArgument, TResult> nested, Func<ScalarArgument, TResult> scalar)
    {
        return scalar(this);
    }
}
