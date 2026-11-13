using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>
/// Presses one of the game's input actions for a frame, whatever keys the player bound it to. It only works in-process,
/// and fails on <c>input: os</c>.
/// </summary>
[Step("press-action")]
[Example("- press-action: EditorUndo")]
public sealed class PressAction : Step
{
    /// <summary>The action, as the game's input settings name it, such as <c>EditorUndo</c> or <c>EditorTestMap</c>.</summary>
    [Arg(Primary = true)]
    public required string Action { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        return context.Input.PressAction(Action);
    }
}
