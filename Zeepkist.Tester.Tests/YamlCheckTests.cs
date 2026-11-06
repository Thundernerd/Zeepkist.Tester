using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class YamlCheckTests
{
    [Fact]
    public void TheYamlParser_Passes_AndNamesTheBuildItLoaded()
    {
        var step = YamlCheck.Run();

        Assert.True(step.Passed, step.ToString());
        Assert.Contains("YamlDotNet", step.Detail);
        Assert.Contains(".NETCoreApp", step.Detail);
    }
}
