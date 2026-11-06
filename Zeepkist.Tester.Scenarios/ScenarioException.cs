using System;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A scenario file can't be read. The message is <c>file:line:col: what's wrong</c>.
/// </summary>
internal sealed class ScenarioException : Exception
{
    public ScenarioException(SourceLocation location, string problem)
        : base($"{location}: {problem}")
    {
        Location = location;
    }

    public SourceLocation Location { get; }
}
