namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// What uGUI finds on top at a point, as the event system's raycast takes it for a player's pointer, which decides
/// whether a press there reaches an Imui window.
/// </summary>
internal enum UguiTop
{
    /// <summary>
    /// There's no event system to ask, as while the game loads a scene.
    /// </summary>
    NoEventSystem,

    /// <summary>
    /// The raycast finds nothing there.
    /// </summary>
    Nothing,

    /// <summary>
    /// Imui's backend, the uGUI graphic its windows draw on, so the point is over one of them.
    /// </summary>
    Imui,

    /// <summary>
    /// Another uGUI object, which takes the pointer before anything under it.
    /// </summary>
    Other,
}
