using System.IO;
using TNRD.Zeepkist.Tester.Scenarios;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Tests;

public class ScenarioYamlTests
{
    [Fact]
    public void AScalar_StaysTheTextItWasWritten_AndKnowsWhereItIs()
    {
        var stream = ScenarioYaml.Load(new StringReader("steps:\n  - wait: on\n"));

        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var step = (YamlMappingNode)((YamlSequenceNode)root["steps"])[0];
        var value = (YamlScalarNode)step["wait"];
        Assert.Equal("on", value.Value);
        Assert.Equal(2, value.Start.Line);
        Assert.Equal(11, value.Start.Column);
    }
}
