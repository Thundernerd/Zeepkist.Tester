using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Clicks the named block where it is on screen, as a player selects it in the level editor.</summary>
[Step("click-block")]
[Requires(typeof(InEditor))]
public sealed class ClickBlock : Step
{
    /// <summary>The block.</summary>
    [Arg(Primary = true)]
    public required Ref<BlockValue> Block { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var point = central.cam.cameraCamera.WorldToScreenPoint(BlockCenter.Of(central, Block.Get(context).Uid));

        // The editor takes a click on its blocks only left of its side panel and below its top bar
        if (point.z <= 0 || point.x < 0 || point.y < 0 || point.x >= Screen.width * 0.75f || point.y >= Screen.height * 0.85f)
            throw new StepFailedException($"{Block} isn't where the editor takes clicks on blocks, but at {point}.");

        yield return context.Input.Click(point);
    }
}
