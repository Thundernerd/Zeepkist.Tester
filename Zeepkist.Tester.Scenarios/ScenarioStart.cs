using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Where a scenario starts: <c>main-menu</c>, <c>editor</c>, <c>editor: { level: file }</c> or <c>free-play: { uid: … }</c>.
/// </summary>
internal sealed class ScenarioStart
{
    public ScenarioStart(string name, IReadOnlyDictionary<string, string> arguments)
    {
        Name = name;
        Arguments = arguments;
    }

    public string Name { get; }

    public IReadOnlyDictionary<string, string> Arguments { get; }
}
