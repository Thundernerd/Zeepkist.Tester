using System;
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
    /// A YamlException's message starts with where it is, as <c>(Line: 3, Col: 5, Idx: 20) - (Line: 3, Col: 5, Idx: 20): </c>.
    /// </summary>
    private static string Problem(YamlException exception)
    {
        const string AfterPlace = "): ";
        var message = exception.Message;
        var place = message.IndexOf(AfterPlace, StringComparison.Ordinal);
        return place < 0 ? message : message.Substring(place + AfterPlace.Length);
    }
}
