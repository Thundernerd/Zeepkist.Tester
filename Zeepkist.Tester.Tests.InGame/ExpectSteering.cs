using System.Collections;
using Rewired;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Checks the soapbox steered one way since <c>watch-steering</c>, such as while <c>hold-action</c> held its action, and
/// stops watching.
/// </summary>
[Step("expect-steering")]
[Requires(typeof(InTestMode))]
public sealed class ExpectSteering : Step
{
    /// <summary>Which way: below 0 for left, and above it for right, as the steering action's value goes.</summary>
    [Arg(Primary = true)]
    public required float Direction { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var car = Object.FindObjectOfType<New_ControlCar>();
        var watch = car == null ? null : car.GetComponent<SteeringWatch>();
        if (watch == null)
        {
            context.Fail("Nothing watched the soapbox steer: watch-steering comes first.");
            yield break;
        }

        var most = watch.Most;
        Object.Destroy(watch);

        // The player's setting turns steering round
        var expected = Mathf.Sign(Direction) * (car.invertSteering ? -1 : 1);
        var steersWith = ReInput.mapping.GetAction(car.GetSteerBasedOnCurrentState().ID)?.name;
        var actual = $"steering at most {most:0.00}, with the soapbox steering by {steersWith}";
        if (Mathf.Abs(most) < 0.1f || Mathf.Sign(most) != expected)
            context.Fail("The soapbox didn't steer that way.", expected < 0 ? "steering left" : "steering right", actual);
        else
            context.Detail = actual;
    }
}
