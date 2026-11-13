using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Scenarios;
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
    public void EveryZlsScenario_LoadsThroughTheHarnessLoader_AsItsConvertedYamlDoes(string file)
    {
        using var folder = new TemporaryFolder();
        var converted = Path.Combine(folder.Path, "converted.ztest.yaml");
        using (var reader = File.OpenText(file))
            File.WriteAllText(converted, LineScenarioConverter.Write(LineScenarioReader.Read(file, reader, ZlsSteps.Registry), ZlsSteps.Registry));

        var scenario = new ScenarioLoader(ZlsSteps.Registry).Load(file);
        var yaml = new ScenarioLoader(ZlsSteps.Registry).Load(converted);

        LineScenarioConverterTests.AssertSameSteps(yaml.Steps.Cast<StepNode>().ToList(), scenario.Steps.Cast<StepNode>().ToList(), File.ReadAllText(converted));
        Assert.Equal(yaml.Use, scenario.Use);
    }
}
