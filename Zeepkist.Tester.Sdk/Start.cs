namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Where a C# test starts. Each is also the state it starts in, by the same name in kebab case.
/// </summary>
public enum Start
{
    /// <summary>A new level in the level editor.</summary>
    Editor,

    /// <summary>The main menu.</summary>
    MainMenu,
}
