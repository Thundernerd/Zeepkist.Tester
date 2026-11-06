using System.IO;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Reads a scenario file's YAML into nodes, where every scalar stays the text it was written as and knows its line and
/// column.
/// </summary>
internal static class ScenarioYaml
{
    public static YamlStream Load(TextReader reader)
    {
        var stream = new YamlStream();
        stream.Load(reader);
        return stream;
    }
}
