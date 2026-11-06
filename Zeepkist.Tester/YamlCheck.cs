using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using TNRD.Zeepkist.Tester.Scenarios;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Makes sure the YamlDotNet build this plugin ships runs in the game, the way scenarios need it to, and names that build.
/// </summary>
internal static class YamlCheck
{
    private const string Expected = "'on' at 2:11, syntax error at 3:5, next to the plugin";

    public static RunStep Run()
    {
        var yaml = typeof(YamlStream).Assembly;
        var actual = $"{Scalar()}, {SyntaxError()}, {Place(yaml)}";
        return new RunStep
        {
            Name = "yaml parser",
            Passed = actual == Expected,
            Detail = BuildName(yaml),
            Expected = Expected,
            Actual = actual,
        };
    }

    private static string Scalar()
    {
        var root = (YamlMappingNode)ScenarioYaml.Load("test", new StringReader("steps:\n  - wait: on\n")).Documents[0].RootNode;
        var step = (YamlMappingNode)((YamlSequenceNode)root["steps"])[0];
        var value = (YamlScalarNode)step["wait"];
        return $"'{value.Value}' at {value.Start.Line}:{value.Start.Column}";
    }

    private static string SyntaxError()
    {
        try
        {
            ScenarioYaml.Load("test", new StringReader("steps:\n  - wait: 2\n    - broken\n"));
            return "no syntax error";
        }
        catch (ScenarioException exception)
        {
            return $"syntax error at {exception.Location.Line}:{exception.Location.Column}";
        }
    }

    /// <summary>
    /// BepInEx loads every plugin into one AppDomain, so the YamlDotNet that's loaded can be another mod's.
    /// </summary>
    private static string Place(Assembly yaml)
    {
        var folder = Path.GetDirectoryName(typeof(YamlCheck).Assembly.Location);
        var yamlFolder = Path.GetDirectoryName(yaml.Location);
        return string.Equals(folder, yamlFolder, StringComparison.OrdinalIgnoreCase) ? "next to the plugin" : "from another folder";
    }

    private static string BuildName(Assembly assembly)
    {
        var framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        return $"{assembly.GetName().Name} {assembly.GetName().Version} {framework} from {assembly.Location}";
    }
}
