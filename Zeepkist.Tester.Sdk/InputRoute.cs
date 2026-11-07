namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// How <see cref="IInput"/> reaches the game.
/// </summary>
public enum InputRoute
{
    /// <summary>
    /// Handed to what reads input from inside the game: in the same frame, with no need for the window in front.
    /// </summary>
    InProcess,

    /// <summary>
    /// What a player's keyboard and mouse do, which needs the window in front.
    /// </summary>
    Os,
}
