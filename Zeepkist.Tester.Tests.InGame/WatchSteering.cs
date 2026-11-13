using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Starts keeping how far the soapbox steers, for <c>expect-steering</c> to check after the steps that steer it.
/// </summary>
[Step("watch-steering")]
[Requires(typeof(InTestMode))]
public sealed class WatchSteering : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var car = Object.FindObjectOfType<New_ControlCar>();
        if (car == null)
        {
            context.Fail("There's no soapbox.");
            yield break;
        }

        var old = car.GetComponent<SteeringWatch>();
        if (old != null)
            Object.DestroyImmediate(old);

        car.gameObject.AddComponent<SteeringWatch>();
    }
}
