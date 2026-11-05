using System;
using TNRD.Zeepkist.Tester.Patches;
using UnityEngine.SceneManagement;
using ZeepSDK.LevelEditor;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Counts the moments a run waits for, so a wait compares a count from before with the count now instead of
/// subscribing for itself, which a step that stops halfway would leave subscribed.
/// </summary>
internal sealed class GameEvents : IDisposable
{
    public GameEvents()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        LevelEditorApi.EnteredLevelEditor += OnEnteredLevelEditor;
        LEV_SaveLoad_ExternalLoad.Loaded += OnLevelLoaded;
        LevelEditorApi.EnteredTestMode += OnEnteredTestMode;
        RacingApi.RoundStarted += OnRoundStarted;
    }

    public int ScenesLoaded { get; private set; }
    public int EditorEntries { get; private set; }
    public int TestModeEntries { get; private set; }

    /// <summary>
    /// Counts each release of the soapbox at the start of a round, in test mode as anywhere else.
    /// </summary>
    public int RoundStarts { get; private set; }

    /// <summary>
    /// Counts levels the editor started loading from a file, which it does when it comes back from test mode.
    /// </summary>
    public int LevelLoads { get; private set; }

    public void Dispose()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        LevelEditorApi.EnteredLevelEditor -= OnEnteredLevelEditor;
        LEV_SaveLoad_ExternalLoad.Loaded -= OnLevelLoaded;
        LevelEditorApi.EnteredTestMode -= OnEnteredTestMode;
        RacingApi.RoundStarted -= OnRoundStarted;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScenesLoaded++;
    }

    private void OnEnteredLevelEditor()
    {
        EditorEntries++;
    }

    private void OnLevelLoaded()
    {
        LevelLoads++;
    }

    private void OnEnteredTestMode()
    {
        TestModeEntries++;
    }

    private void OnRoundStarted()
    {
        RoundStarts++;
    }
}
