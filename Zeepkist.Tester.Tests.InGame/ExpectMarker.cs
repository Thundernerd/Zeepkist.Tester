using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks the text of a named value.</summary>
[Step("expect-marker")]
public sealed class ExpectMarker : Step
{
    /// <summary>Which one.</summary>
    [Arg]
    public required Ref<Marker> Marker { get; set; }

    /// <summary>What it should say.</summary>
    [Arg]
    public required string Text { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var actual = Marker.Get(context).Text;
        if (actual == Text)
            context.Pass("The marker is right.", Text, actual);
        else
            context.Fail("The marker is wrong.", Text, actual);

        yield break;
    }
}
