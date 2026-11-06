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
}
