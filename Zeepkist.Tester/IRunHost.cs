using System.Collections;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The game, as <see cref="ScenarioRunner"/> needs it.
/// </summary>
internal interface IRunHost
{
    public ManualLogSource Logger { get; }

    /// <summary>
    /// Seconds of real time, so a paused game doesn't stop a wait from giving up.
    /// </summary>
    public float Now { get; }

    public IInput Input { get; }

    public IGameEvents Game { get; }

    /// <summary>
    /// The level editor, or null outside it.
    /// </summary>
    public LEV_LevelEditorCentral Editor { get; }

    /// <summary>
    /// Takes the game to where a scenario starts. An exception from it leaves the game somewhere unknown.
    /// </summary>
    public IEnumerator EnterStart(ScenarioStart start, string scenarioFile);

    public IEnumerator Screenshot(string name);
}
