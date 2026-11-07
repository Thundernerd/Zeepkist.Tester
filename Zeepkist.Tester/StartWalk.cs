using System;
using System.Collections;
using System.IO;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Racing;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Takes the game to where a scenario starts the way a player gets there, by way of the main menu, so each scenario gets
/// a new level or round whatever the last one left behind. Run it through <see cref="GuardedRoutine"/>: it throws
/// <see cref="StartFailedException"/> when it can't get there, because each wait gives up after <see cref="WaitSeconds"/>.
/// </summary>
internal sealed class StartWalk
{
    private const float WaitSeconds = 120;

    // What a player can't click any sooner: a menu that's opening, and the editor's UI fading in after its level is ready
    private const float SettleSeconds = 2;

    private readonly GameEvents _game;
    private readonly ManualLogSource _logger;
    private readonly Action<RunStep> _step;
    private bool _pastStartScreen;

    public StartWalk(GameEvents game, ManualLogSource logger, Action<RunStep> step)
    {
        _game = game;
        _logger = logger;
        _step = step;
    }

    /// <param name="start">Where to go.</param>
    /// <param name="scenarioFile">What a level file in <paramref name="start"/> is written relative to.</param>
    public IEnumerator Enter(ScenarioStart start, string scenarioFile)
    {
        yield return _pastStartScreen ? ToMainMenu() : FromStartScreen();
        yield return AwaitMainMenu();

        var location = GameLocation.MainMenu;
        if (start.State == StartState.Editor)
        {
            location = GameLocation.Editor;
            yield return NewLevel();
            if (start.LevelPath(scenarioFile) is { } level)
                yield return OpenLevel(level);
        }
        else if (start.State == StartState.FreePlay)
        {
            location = GameLocation.FreePlay;
            yield return FreePlay(start.Arguments[start.State.Argument]);
        }

        if (GameLocations.Current != location)
            throw new StartFailedException($"{start.State.Name}: the game is in {SceneManager.GetActiveScene().name}");
    }

    private IEnumerator FromStartScreen()
    {
        PressAnyKeyToStartGame startScreen = null;
        yield return AwaitObject(() => startScreen = Find<PressAnyKeyToStartGame>(), "the start screen");
        yield return Until(() => startScreen.disableThisIntroPanel.activeInHierarchy && PlayerManager.Instance != null, "press any key");

        // Skips the main menu's one-time warning panels for this session, without changing settings
        PlayerManager.Instance.modPanelHasAlreadyBeenDisplayed = true;
        PlayerManager.Instance.epilepsyPanelHasAlreadyBeenDisplayed = true;
        startScreen.LoadNextLevel();
        _pastStartScreen = true;
        _step(RunStep.Pass("start screen"));
    }

    private IEnumerator ToMainMenu()
    {
        var from = GameLocations.Current;
        switch (from)
        {
            case GameLocation.MainMenu:
                yield break;
            case GameLocation.Editor:
                QuitEditor();
                break;
            case GameLocation.TestMode:
                var loadsBefore = _game.LevelLoads;
                yield return QuitRound();
                yield return Until(() => IsLevelBack(loadsBefore), "the level editor, from test mode");
                QuitEditor();
                break;
            case GameLocation.FreePlay:
                yield return QuitRound();
                yield return Until(() => GameLocations.Current != GameLocation.FreePlay, "a menu, from free play");
                GoHome();
                break;
            case GameLocation.OtherMenu:
                GoHome();
                break;
            default:
                throw new StartFailedException($"the main menu: no way there from {SceneManager.GetActiveScene().name}");
        }
    }

    /// <summary>
    /// Back to the main menu without saving. The game still writes an autosave, which the runner puts back after a run.
    /// </summary>
    private static void QuitEditor()
    {
        var central = ComponentCache.Get<LEV_LevelEditorCentral>();
        central.pause.QuitToMenu();
        SayYesToUnsavedContent(central);
    }

    private static void SayYesToUnsavedContent(LEV_LevelEditorCentral central)
    {
        if (central.unsavedContentPopup.IsOpen)
            central.unsavedContentPopup.Yes();
    }

    /// <summary>
    /// Opens the pause menu and presses Quit, which goes back to the editor from a test level and to a menu from free play.
    /// </summary>
    private static IEnumerator QuitRound()
    {
        var pauseMenu = Find<PauseMenuUI>();
        if (pauseMenu == null)
            throw new StartFailedException("the level's pause menu: not found");

        pauseMenu.Open(true);
        yield return null;
        pauseMenu.OnQuit();
    }

    /// <summary>
    /// What the level select's Home button does.
    /// </summary>
    private static void GoHome()
    {
        SceneManager.LoadScene(GameLocations.MainMenuScene);
    }

    private IEnumerator AwaitMainMenu()
    {
        yield return AwaitObject(() => Find<LevelEditorUI>(), "the main menu");
        yield return new WaitForSecondsRealtime(SettleSeconds);
        _step(RunStep.Pass("main menu"));
    }

    private IEnumerator NewLevel()
    {
        var entriesBefore = _game.EditorEntries;
        PressLevelEditorButton(Find<LevelEditorUI>());
        yield return Until(() => _game.EditorEntries > entriesBefore, "the level editor");
        yield return new WaitForSecondsRealtime(SettleSeconds);
        _step(RunStep.Pass("new level"));
    }

    /// <summary>
    /// Clicks the main menu's Level Editor button, or calls what it calls when no button is wired to it.
    /// </summary>
    private void PressLevelEditorButton(LevelEditorUI menu)
    {
        foreach (var button in Object.FindObjectsOfType<GenericButton>(true))
        {
            for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == menu
                    && button.onClick.GetPersistentMethodName(i) == nameof(LevelEditorUI.StartLevelEditor))
                {
                    button.onClick.Invoke();
                    return;
                }
            }
        }

        _logger.LogWarning($"{TestRun.Marker} no Level Editor button found, starting the editor directly");
        menu.StartLevelEditor();
    }

    /// <summary>
    /// What the load panel's button does once it has a file, with the load panel open because only that lets the load run.
    /// ZeepSDK has no way to do this.
    /// </summary>
    private IEnumerator OpenLevel(string path)
    {
        if (!File.Exists(path))
            throw new StartFailedException($"the level file: there's no {path}");

        var central = ComponentCache.Get<LEV_LevelEditorCentral>();
        central.tool.EnableLoadPanel();
        SayYesToUnsavedContent(central);
        central.validation.BreakLock(false, null, "SaveLoad", false);
        central.saveload.ExternalLoad(path, false, false);
        central.manager.testLevelName = Path.GetFileNameWithoutExtension(path);

        // The game swallows what goes wrong reading the file, and stays loading
        yield return Until(() => !central.saveload.isLoading, "the level file loaded");
        yield return new WaitForSecondsRealtime(SettleSeconds);

        // The game opens a file that isn't a level as an empty one, and a scenario that wants one writes start: editor
        if (central.undoRedo.allBlocksDictionary.Count == 0)
            throw new StartFailedException($"the level file: the game found no blocks in {path}");

        _step(RunStep.Pass("level file"));
    }

    private IEnumerator FreePlay(string uid)
    {
        var roundsBefore = _game.RoundStarts;
        Result result = null;
        Exception error = null;
        yield return RacingApi.LoadTrackInFreePlayAsync(uid).ToCoroutine(loaded => result = loaded, exception => error = exception);
        if (error != null)
            throw new StartFailedException($"free play: {error.Message}");

        if (result.IsFailed)
            throw new StartFailedException($"free play: {result.Errors[0].Message}: {uid}");

        yield return Until(() => _game.RoundStarts > roundsBefore, "the round in free play");
        _step(RunStep.Pass("free play"));
    }

    /// <summary>
    /// Whether the editor has loaded a level since <paramref name="loadsBefore"/> and placed all its blocks, as it does
    /// coming back from test mode.
    /// </summary>
    private bool IsLevelBack(int loadsBefore)
    {
        // Only looked up once a load started, so the wait doesn't search the scene every frame before then
        return _game.LevelLoads > loadsBefore
            && ComponentCache.Get<LEV_LevelEditorCentral>() is { } central
            && !central.saveload.isLoading;
    }

    private static T Find<T>()
        where T : Object
    {
        return Object.FindObjectOfType<T>(true);
    }

    /// <summary>
    /// Looks for an object once now and once after each scene load, rather than searching the scene every frame.
    /// </summary>
    private IEnumerator AwaitObject(Func<Object> find, string what)
    {
        var checkedAt = -1;
        return Until(
            () =>
            {
                if (checkedAt == _game.ScenesLoaded)
                    return false;

                checkedAt = _game.ScenesLoaded;
                return find() != null;
            },
            what);
    }

    private static IEnumerator Until(Func<bool> condition, string what)
    {
        return TimedWait.Until(condition, what, WaitSeconds, () => Time.realtimeSinceStartup);
    }
}
