using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.States;

/// <summary>The main menu.</summary>
[State("main-menu", Hint = "Start with `start: main-menu`.")]
public sealed class InMainMenu : IState
{
    public bool IsMet(IStepContext context)
    {
        return GameLocations.Current == GameLocation.MainMenu;
    }
}
