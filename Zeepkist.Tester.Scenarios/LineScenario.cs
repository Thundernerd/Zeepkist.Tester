using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A <c>.zlstest</c> file, read line by line into the steps of the registry it now means, with its comments.
/// </summary>
internal sealed class LineScenario
{
    public LineScenario(string file, IReadOnlyList<LineEntry> entries)
    {
        File = file;
        Entries = entries;
    }

    public string File { get; }

    /// <summary>
    /// Every line but those a step spans, in order.
    /// </summary>
    public IReadOnlyList<LineEntry> Entries { get; }
}
