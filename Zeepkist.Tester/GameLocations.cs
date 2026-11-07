using UnityEngine.SceneManagement;

namespace TNRD.Zeepkist.Tester;

internal static class GameLocations
{
    public const string MainMenuScene = "3D_MainMenu";

    public static GameLocation Current
    {
        get
        {
            var manager = PlayerManager.Instance;
            return Of(SceneManager.GetActiveScene().name, manager != null && manager.loader.GlobalLevel.IsTestLevel);
        }
    }

    /// <summary>
    /// Test mode and free play are the same scene, and only the level it plays tells them apart.
    /// </summary>
    public static GameLocation Of(string scene, bool isTestLevel)
    {
        return scene switch
        {
            MainMenuScene => GameLocation.MainMenu,
            "SelectLevelScene" or "Avonturenkaart" => GameLocation.OtherMenu,
            "LevelEditor2" => GameLocation.Editor,
            "GameScene" => isTestLevel ? GameLocation.TestMode : GameLocation.FreePlay,
            _ => GameLocation.Elsewhere,
        };
    }
}
