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
    /// The color of every pixel of the last screenshot when they were all the same, or null.
    /// </summary>
    public string LastBlankColor { get; }

    /// <summary>
    /// Takes the game to where a scenario starts. An exception from it leaves the game somewhere unknown.
    /// </summary>
    /// <param name="start">Where to go.</param>
    /// <param name="levelFolder">What a level file in <paramref name="start"/> is written relative to.</param>
    public IEnumerator EnterStart(ScenarioStart start, string levelFolder);

    /// <summary>
    /// With <paramref name="covered"/>, fills the frame with one color just before the capture, so the screenshot is blank.
    /// </summary>
    public IEnumerator Screenshot(string name, bool covered = false);
}
