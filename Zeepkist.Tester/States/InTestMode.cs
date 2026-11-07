using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.States;

/// <summary>
/// Playing the editor's level in test mode.
/// </summary>
[State("test-mode", Hint = "Add `test-mode` first.")]
public sealed class InTestMode : IState
{
    public bool IsMet(IStepContext context)
    {
        return GameLocations.Current == GameLocation.TestMode;
    }
}
