using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Saves what's on screen in the run's results, and fails if it came out blank, because then it shows nothing.</summary>
[Step("screenshot")]
[Example("- screenshot: running")]
public sealed class Screenshot : Step
{
    /// <summary>A name for the file, which goes after the scenario's name.</summary>
    [Arg(Primary = true)]
    public required FileName Name { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        yield return context.Screenshot(Name);
        if (context.LastScreenshotBlankColor is { } color)
            context.Fail($"The screenshot is blank: every pixel is {color}.", "more than one color", $"every pixel {color}");
    }
}
