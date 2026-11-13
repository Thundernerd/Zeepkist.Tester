using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>
/// Holds one of the game's input actions for a while, whatever keys the player bound it to, such as to steer the soapbox,
/// and lets go of it when the time is up. It only works in-process, and fails on <c>input: os</c>.
/// </summary>
[Step("hold-action")]
[Example("- hold-action: { action: Horizontal, for: 1 }")]
[Example("- hold-action: { action: Horizontal, value: -1, for: 500ms }")]
public sealed class HoldAction : Step
{
    /// <summary>The action, as the game's input settings name it, such as <c>Horizontal</c> for steering.</summary>
    [Arg(Primary = true)]
    public required string Action { get; set; }

    /// <summary>How long to hold it.</summary>
    [Arg]
    public required Seconds For { get; set; }

    /// <summary>What to hold it at: 1 for a button, and from -1 to 1 for an axis, where below 0 steers left.</summary>
    [Arg(Default = 1)]
    [InRange(-1, 1)]
    public float Value { get; set; } = 1;

    public override IEnumerator Run(IStepContext context)
    {
        return context.Input.HoldAction(Action, For, Value);
    }
}
