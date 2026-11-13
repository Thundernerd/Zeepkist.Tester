using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A <c>.zlstest</c> file, read line by line into the steps of the registry it now means, with its comments.
/// </summary>
internal sealed class LineScenario
{
    public LineScenario(IReadOnlyList<LineEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>
    /// Every line but those a step spans, in order.
    /// </summary>
    public IReadOnlyList<LineEntry> Entries { get; }

    /// <summary>
    /// Its mods' namespaces, in the order its steps first use them, which is what the converted file's <c>use:</c> lists.
    /// </summary>
    public IReadOnlyList<string> Use => Steps.Select(step => step.Step.Namespace).Where(name => name.Length > 0).Distinct().ToList();

    /// <summary>
    /// Its steps, without its comments and blank lines.
    /// </summary>
    public IReadOnlyList<StepNode> Steps => Entries.Select(entry => entry.Step).Where(step => step != null).ToList();

    /// <summary>
    /// The scenario its converted file would be: the old format has no header, so it starts in the editor with input in process.
    /// </summary>
    public Scenario ToScenario(string file)
    {
        return new Scenario(file, null, Use, ScenarioStart.Default, InputRoute.InProcess, null, Steps);
    }
}
