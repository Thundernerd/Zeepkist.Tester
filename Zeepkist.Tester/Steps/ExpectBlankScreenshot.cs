using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Covers the frame in one color and takes a screenshot, to check that a run finds a blank one.</summary>
[Step("expect-blank-screenshot")]
[Example("- expect-blank-screenshot: covered")]
public sealed class ExpectBlankScreenshot : Step
{
    /// <summary>A name for the file, which goes after the scenario's name.</summary>
    [Arg(Primary = true)]
    public required FileName Name { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        yield return context.Screenshot(Name, covered: true);
        if (context.LastScreenshotBlankColor is { } color)
            context.Pass($"Every pixel is {color}.", "blank", "blank");
        else
            context.Fail("The screen was covered in one color, but the screenshot isn't blank.", "blank", "more than one color");
    }
}
