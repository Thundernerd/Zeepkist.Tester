using Xunit;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Tests;

public class YamlCheckTests
{
    [Fact]
    public void TheYamlParser_Passes_WhenYamlDotNetIsInThePlugin_AndNamesWhereItIs()
    {
        var step = YamlCheck.Run(typeof(YamlStream).Assembly);

        Assert.True(step.Passed, step.ToString());
        Assert.Contains("YamlDotNet in YamlDotNet", step.Detail);
        Assert.Contains(".NETCoreApp", step.Detail);
    }

    [Fact]
    public void TheYamlParser_Fails_WhenYamlDotNetIsNotInThePlugin()
    {
        var step = YamlCheck.Run(typeof(YamlCheck).Assembly);

        Assert.False(step.Passed);
        Assert.EndsWith("not merged into the plugin", step.Actual);
    }
}
