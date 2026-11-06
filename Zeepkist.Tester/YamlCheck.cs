using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Makes sure the parser and the YamlDotNet merged into this plugin run in the game, the way scenarios need them to.
/// </summary>
internal static class YamlCheck
{
    private const string Expected = "'on' at 2:12, syntax error at 3:5, merged into the plugin";

    private static readonly StepDescriptor _check = new StepDescriptor
    {
        Name = "check",
        Namespace = string.Empty,
        Arguments =
        [
            new ArgumentDescriptor
            {
                Name = "value",
                PropertyName = nameof(CheckStep.Value),
                Type = new ArgumentType { Kind = ArgumentKind.String },
                IsPrimary = true,
            },
        ],
    };

    /// <param name="plugin">The assembly YamlDotNet has to be merged into.</param>
    public static RunStep Run(Assembly plugin)
    {
        var yaml = typeof(YamlStream).Assembly;
        var actual = $"{Scalar()}, {SyntaxError()}, {Place(yaml, plugin)}";
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
        var node = (StepNode)Parse("steps:\n  - check: on\n").Steps[0];
        var step = (CheckStep)StepBinder.Bind(node, typeof(CheckStep), new ScenarioValues());
        var location = node.Arguments.Values["value"].Location;
        return $"'{step.Value}' at {location.Line}:{location.Column}";
    }

    private static string SyntaxError()
    {
        try
        {
            Parse("steps:\n  - check: 2\n    - broken\n");
            return "no syntax error";
        }
        catch (ScenarioException exception)
        {
            return $"syntax error at {exception.Location.Line}:{exception.Location.Column}";
        }
    }

    private static Scenario Parse(string yaml)
    {
        return ScenarioParser.Parse("yaml parser", new StringReader(yaml), [_check]);
    }

    /// <summary>
    /// BepInEx loads every plugin into one AppDomain, so a YamlDotNet that isn't merged in can be another mod's.
    /// </summary>
    private static string Place(Assembly yaml, Assembly plugin)
    {
        return yaml == plugin ? "merged into the plugin" : "not merged into the plugin";
    }

    private static string BuildName(Assembly assembly)
    {
        var framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        return $"YamlDotNet in {assembly.GetName().Name} {assembly.GetName().Version} {framework} from {assembly.Location}";
    }

    /// <summary>
    /// Has no <see cref="StepAttribute"/>, so it isn't a step of the registry.
    /// </summary>
    private sealed class CheckStep : Step
    {
        public string Value { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }
}
