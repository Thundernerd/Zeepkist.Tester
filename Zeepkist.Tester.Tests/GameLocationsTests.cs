using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class GameLocationsTests
{
    [Theory]
    [InlineData("3D_MainMenu", false, "MainMenu")]
    [InlineData("SelectLevelScene", false, "OtherMenu")]
    [InlineData("Avonturenkaart", false, "OtherMenu")]
    [InlineData("LevelEditor2", false, "Editor")]
    [InlineData("LevelEditor2", true, "Editor")]
    [InlineData("GameScene", true, "TestMode")]
    [InlineData("GameScene", false, "FreePlay")]
    [InlineData("Online Lobby", false, "Elsewhere")]
    public void AScene_IsALocation(string scene, bool isTestLevel, string location)
    {
        Assert.Equal(location, GameLocations.Of(scene, isTestLevel).ToString());
    }
}
