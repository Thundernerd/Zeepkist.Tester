namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Where in the game a run is, which decides the way from there to where the next scenario starts.
/// </summary>
internal enum GameLocation
{
    /// <summary>
    /// A scene a run doesn't know the way back from, such as the online lobby.
    /// </summary>
    Elsewhere,
    MainMenu,

    /// <summary>
    /// A menu that leaving a level ends up in, such as the level select.
    /// </summary>
    OtherMenu,
    Editor,
    TestMode,
    FreePlay,
}
