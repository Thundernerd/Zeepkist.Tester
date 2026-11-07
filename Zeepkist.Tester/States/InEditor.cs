using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.States;

/// <summary>
/// The level editor, not in test mode.
/// </summary>
[State("editor", Hint = "Start with `start: editor`, or add `back-to-editor`.")]
public sealed class InEditor : IState
{
    public bool IsMet(IStepContext context)
    {
        return GameLocations.Current == GameLocation.Editor;
    }
}
