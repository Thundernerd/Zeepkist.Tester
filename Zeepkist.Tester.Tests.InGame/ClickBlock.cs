using System.Collections;
using System.Linq;
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

        var block = central.GetBlock(context, Block);
        var point = FindPoint(central, block)
            ?? throw new StepFailedException($"No part of {Block} is where the editor takes a click on it.");

        yield return context.Input.Click(point);
    }

    /// <summary>
    /// The middle of one of the block's parts that the editor's own raycast hits first, as it finds what's under the mouse.
    /// </summary>
    private static Vector2? FindPoint(LEV_LevelEditorCentral central, BlockProperties block)
    {
        var click = central.click;
        var camera = central.cam.cameraCamera;
        foreach (var collider in block.GetComponentsInChildren<Collider>().Where(collider => ((1 << collider.gameObject.layer) & click.buildingLayer) != 0))
        {
            var point = camera.WorldToScreenPoint(collider.bounds.center);

            // The editor takes a click on its blocks only left of its side panel and below its top bar
            if (point.z <= 0 || point.x < 0 || point.y < 0 || point.x >= Screen.width * 0.75f || point.y >= Screen.height * 0.85f)
                continue;

            var ray = camera.ScreenPointToRay(point);
            if (Physics.Raycast(ray, 9999999f, click.gizmoLayer))
                continue;

            if (Physics.Raycast(ray, out var hit, 9999999f, click.buildingLayer) && hit.transform.parent == block.transform)
                return new Vector2(point.x, point.y);
        }

        return null;
    }
}
