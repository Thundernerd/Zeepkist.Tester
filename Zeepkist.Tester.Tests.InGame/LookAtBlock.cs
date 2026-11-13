using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Moves the editor's camera to look down at the named block from in front of it.</summary>
[Step("look-at-block")]
[Requires(typeof(InEditor))]
public sealed class LookAtBlock : Step
{
    /// <summary>The block.</summary>
    [Arg(Primary = true)]
    public required Ref<BlockValue> Block { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var center = BlockCenter.Of(central.GetBlock(context, Block));
        central.cam.transform.position = center + new Vector3(0, 24, -24);
        var camera = central.cam.cameraTransform;
        camera.rotation = Quaternion.LookRotation(center - camera.position);

        // The editor finds what's under the mouse as it updates
        yield return null;
    }
}
