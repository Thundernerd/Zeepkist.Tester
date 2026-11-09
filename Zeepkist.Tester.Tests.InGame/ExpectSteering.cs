using System.Collections;
using Rewired;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Holds an input action with <c>context.Input</c>, and checks the soapbox steers that way while it's held.
/// </summary>
[Step("expect-steering")]
[Requires(typeof(InTestMode))]
public sealed class ExpectSteering : Step
{
    /// <summary>The game's input action to hold.</summary>
    [Arg(Primary = true)]
    public required string Action { get; set; }

    /// <summary>What to hold it at: below 0 steers left.</summary>
    [Arg]
    public required float Value { get; set; }

    /// <summary>How long.</summary>
    [Arg]
    public required Seconds For { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var car = Object.FindObjectOfType<New_ControlCar>();
        if (car == null)
        {
            context.Fail("There's no soapbox.");
            yield break;
        }

        var hold = context.Input.HoldAction(Action, For, Value);
        var most = 0f;
        while (hold.MoveNext())
        {
            yield return hold.Current;
            if (Mathf.Abs(car.lerpedSteering) > Mathf.Abs(most))
                most = car.lerpedSteering;
        }

        // The player's setting turns steering round
        var expected = Mathf.Sign(Value) * (car.invertSteering ? -1 : 1);
        var steersWith = ReInput.mapping.GetAction(car.GetSteerBasedOnCurrentState().ID)?.name;
        var actual = $"steering at most {most:0.00}, with the soapbox steering by {steersWith}";
        if (Mathf.Abs(most) < 0.1f || Mathf.Sign(most) != expected)
            context.Fail("The soapbox didn't steer that way.", expected < 0 ? "steering left" : "steering right", actual);
        else
            context.Detail = actual;
    }
}
