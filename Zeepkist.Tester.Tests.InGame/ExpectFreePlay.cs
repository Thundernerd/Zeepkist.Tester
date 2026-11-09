using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks free play is playing a level, past the state the tester declares.</summary>
[Step("expect-free-play")]
[Requires(typeof(InFreePlay))]
public sealed class ExpectFreePlay : Step
{
    /// <summary>The level's UID.</summary>
    [Arg(Primary = true)]
    public required string Uid { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var actual = PlayerManager.Instance.loader.GlobalLevel.UID;
        if (actual == Uid)
            context.Pass($"Playing {actual}", Uid, actual);
        else
            context.Fail("Playing another level", Uid, actual);

        yield break;
    }
}
