using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A place <c>start:</c> can name. Each is also the state a scenario starts in, by the same name.
/// </summary>
internal sealed class StartState
{
    public static readonly StartState MainMenu = new StartState("main-menu", null, false);
    public static readonly StartState Editor = new StartState("editor", "level", false);
    public static readonly StartState FreePlay = new StartState("free-play", "uid", true);

    private StartState(string name, string argument, bool needsArgument)
    {
        Name = name;
        Argument = argument;
        NeedsArgument = needsArgument;
    }

    public static IReadOnlyList<StartState> All { get; } = [MainMenu, Editor, FreePlay];

    public string Name { get; }

    /// <summary>
    /// The only argument it takes, or null.
    /// </summary>
    public string Argument { get; }

    public bool NeedsArgument { get; }
}
