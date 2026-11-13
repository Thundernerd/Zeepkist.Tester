using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Reads a scenario file's YAML into nodes, where every scalar stays the text it was written as and knows its line and
/// column.
/// </summary>
internal static class ScenarioYaml
{
    /// <exception cref="ScenarioException">The file isn't valid YAML.</exception>
    public static YamlStream Load(string file, TextReader reader)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(reader);
        }
        catch (YamlException exception)
        {
            throw new ScenarioException(new SourceLocation(file, exception.Start.Line, exception.Start.Column), Problem(exception));
        }

        return stream;
    }

    /// <summary>
    /// A YamlException's Message is only what's wrong: the place is added by its ToString, and the scenario's own
    /// location takes its place. The syntax-error fixtures notice if an update moves the place into Message.
    /// </summary>
    private static string Problem(YamlException exception)
    {
        var problem = exception.Message;
        return problem.Contains("flow") ? $"{problem} Inside {{ }} or [ ], quote a value with ${{ or a colon in it." : problem;
    }
}
