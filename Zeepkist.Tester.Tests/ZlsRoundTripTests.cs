using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Tests.Zls;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Every <c>.zlstest</c> ZeepLogicScript has, copied from its <c>Zeepkist.LogicScripting.InGameTests/Scenarios/</c> and <c>.scratch/ingame/</c>.
/// </summary>
public class ZlsRoundTripTests
{
    private static readonly string _folder = Path.Combine("Zls");

    public static TheoryData<string> Files => [.. Directory.GetFiles(_folder, "*.zlstest", SearchOption.AllDirectories).Order()];

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryZlsScenario_ConvertsToYaml_ThatParsesToTheSameSteps(string file)
    {
        LineScenarioConverterTests.RoundTrip(File.ReadAllText(file));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryZlsScenario_LoadsAsItIs_ThroughTheHarnessLoader(string file)
    {
        var scenario = new ScenarioLoader(ZlsSteps.Registry).Load(file);

        Assert.NotEmpty(scenario.Steps);
    }
}
