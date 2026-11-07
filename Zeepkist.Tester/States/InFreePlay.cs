using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.States;

/// <summary>Playing a level in free play, not in test mode.</summary>
[State("free-play", Hint = "Start with `start: free-play`.")]
public sealed class InFreePlay : IState
{
    public bool IsMet(IStepContext context)
    {
        return GameLocations.Current == GameLocation.FreePlay;
    }
}
