using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Defines a named value, which ends with the pass or file it's defined in.</summary>
[Step("set-marker")]
public sealed class SetMarker : Step
{
    /// <summary>Its name.</summary>
    [Arg]
    public required Name<Marker> As { get; set; }

    /// <summary>Its text.</summary>
    [Arg]
    public required string Text { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        As.Set(context, new Marker { Text = Text });
        yield break;
    }
}
