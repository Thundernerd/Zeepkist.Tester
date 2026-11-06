using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using TNRD.Zeepkist.Tester.Scenarios;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Makes sure the YamlDotNet build this plugin ships runs in the game, the way scenarios need it to, and names that build.
/// </summary>
internal static class YamlCheck
{
    private const string Expected = "'on' at 2:11, syntax error at 3:5";

    public static RunStep Run()
    {
        var actual = $"{Scalar()}, {SyntaxError()}";
        return new RunStep
        {
            Name = "yaml parser",
            Passed = actual == Expected,
            Detail = Build(typeof(YamlStream).Assembly),
            Expected = Expected,
            Actual = actual,
        };
    }

    private static string Scalar()
    {
        var root = (YamlMappingNode)ScenarioYaml.Load(new StringReader("steps:\n  - wait: on\n")).Documents[0].RootNode;
        var step = (YamlMappingNode)((YamlSequenceNode)root["steps"])[0];
        var value = (YamlScalarNode)step["wait"];
        return $"'{value.Value}' at {value.Start.Line}:{value.Start.Column}";
    }

    private static string SyntaxError()
    {
        try
        {
            ScenarioYaml.Load(new StringReader("steps:\n  - wait: 2\n    - broken\n"));
            return "no syntax error";
        }
        catch (YamlException exception)
        {
            return $"syntax error at {exception.Start.Line}:{exception.Start.Column}";
        }
    }

    private static string Build(Assembly assembly)
    {
        var framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        return $"{assembly.GetName().Name} {assembly.GetName().Version} {framework} from {assembly.Location}";
    }
}
