using UnityEngine.SceneManagement;

namespace TNRD.Zeepkist.Tester;

internal static class GameLocations
{
    public const string MainMenuScene = "3D_MainMenu";

    private const string RoundScene = "GameScene";

    public static GameLocation Current
    {
        get
        {
            var scene = SceneManager.GetActiveScene().name;
            return Of(scene, scene == RoundScene && PlayerManager.Instance.loader.GlobalLevel.IsTestLevel);
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
            RoundScene => isTestLevel ? GameLocation.TestMode : GameLocation.FreePlay,
            _ => GameLocation.Elsewhere,
        };
    }
}
