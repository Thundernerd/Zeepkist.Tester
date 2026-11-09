using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Drags sideways with the right mouse button in the level editor, which should turn its camera.</summary>
[Step("turn-camera")]
[Requires(typeof(InEditor))]
public sealed class TurnCamera : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var camera = central.cam.cameraTransform;
        var before = camera.rotation;
        var middle = new Vector2(Screen.width * 0.375f, Screen.height * 0.425f);
        yield return context.Input.Drag(middle, [new DragMove { By = 100, Over = 5, Sideways = true }], MouseButton.Right);

        var turned = Quaternion.Angle(before, camera.rotation);
        if (turned > 1)
            context.Pass($"The camera turned {turned:0.#}°", null, null);
        else
            context.Fail("The drag didn't turn the camera", "more than 1°", turned.ToString("0.#", CultureInfo.InvariantCulture) + "°");
    }
}
