using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>
/// Places a start block on the ground below the camera, facing the way it looks. It's one undo step, like placing it by hand.
/// </summary>
[Step("place-start")]
[Example("- place-start")]
[Requires(typeof(InEditor))]
public sealed class PlaceStart : Step
{
    private const float QuarterTurn = 90;

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var startId = central.BlockTypes().FindIndex(block => block != null && block.isStart);
        if (startId < 0)
        {
            context.Fail("The game has no start block.");
            yield break;
        }

        var yaw = Mathf.Round(central.cam.cameraTransform.eulerAngles.y / QuarterTurn) * QuarterTurn;
        var placed = BlockPlacing.Place(central, startId, BlockPlacing.NewUid(startId), yaw, cellsToTheSide: 0, "Test: place start");
        context.Detail = $"Block type {startId} at {placed.transform.position}, facing {yaw}°";
        yield return null;
    }
}
