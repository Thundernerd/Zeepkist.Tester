using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// The arguments of a step or a nested class that were written, by name.
/// </summary>
internal sealed class ObjectArgument : ArgumentValue
{
    public ObjectArgument(SourceLocation location, IReadOnlyDictionary<string, ArgumentValue> values)
        : base(location)
    {
        Values = values;
    }

    public IReadOnlyDictionary<string, ArgumentValue> Values { get; }
}
