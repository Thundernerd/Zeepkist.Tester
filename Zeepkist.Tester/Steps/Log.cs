using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Writes a line to the results and the log, to say what a scenario has got to.</summary>
[Step("log")]
[Example("- log: The window is open")]
public sealed class Log : Step
{
    /// <summary>The line to write.</summary>
    [Arg(Primary = true)]
    public required string Message { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        context.Detail = Message;
        context.Logger.LogInfo($"{TestRun.Marker} {context.ScenarioName}: {Message}");
        yield break;
    }
}
