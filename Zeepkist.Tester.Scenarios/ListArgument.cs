using System;
using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

internal sealed class ListArgument : ArgumentValue
{
    public ListArgument(SourceLocation location, IReadOnlyList<ArgumentValue> items)
        : base(location)
    {
        Items = items;
    }

    public IReadOnlyList<ArgumentValue> Items { get; }

    public override TResult Match<TResult>(Func<ListArgument, TResult> list, Func<ObjectArgument, TResult> nested, Func<ScalarArgument, TResult> scalar)
    {
        return list(this);
    }
}
