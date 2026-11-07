using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Leaves a round the way a player does.
/// </summary>
internal static class RoundControls
{
    public static PauseMenuUI FindPauseMenu()
    {
        return Object.FindObjectOfType<PauseMenuUI>(true);
    }

    /// <summary>
    /// Opens the pause menu and presses Quit, which goes back to the editor from a test level and to a menu from free play.
    /// </summary>
    public static IEnumerator Quit(PauseMenuUI pauseMenu)
    {
        pauseMenu.Open(true);
        yield return null;
        pauseMenu.OnQuit();
    }

    /// <summary>
    /// Whether the editor has loaded a level since <paramref name="loadsBefore"/> and placed all its blocks, as it does
    /// coming back from test mode.
    /// </summary>
    public static bool IsLevelBack(IGameEvents game, int loadsBefore)
    {
        // Only looked up once a load started, so the wait doesn't search the scene every frame before then
        return game.LevelLoads > loadsBefore
            && ComponentCache.Get<LEV_LevelEditorCentral>() is { } central
            && !central.saveload.isLoading;
    }
}
