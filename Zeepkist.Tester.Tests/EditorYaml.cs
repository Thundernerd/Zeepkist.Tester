using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// A YAML file as an editor's schema check sees it: YAML 1.2's core schema, as Red Hat YAML reads it, where a plain
/// <c>2</c> is a number, <c>true</c> a boolean and <c>on</c> text, and anything quoted is text.
/// </summary>
internal static class EditorYaml
{
    private static readonly Regex _null = new Regex(@"^(~|null|Null|NULL|)$");
    private static readonly Regex _bool = new Regex(@"^(true|True|TRUE|false|False|FALSE)$");
    private static readonly Regex _int = new Regex(@"^[-+]?[0-9]+$");
    private static readonly Regex _float = new Regex(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$");

    public static JsonElement ToJson(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var json = ToNode(stream.Documents[0].RootNode);
        return JsonSerializer.SerializeToElement(json);
    }

    private static JsonNode ToNode(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
                var json = new JsonObject();
                foreach (var entry in map.Children)
                    json[((YamlScalarNode)entry.Key).Value] = ToNode(entry.Value);

                return json;
            case YamlSequenceNode list:
                return new JsonArray(list.Select(ToNode).ToArray());
            default:
                return Scalar((YamlScalarNode)node);
        }
    }

    private static JsonNode Scalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? string.Empty;
        if (scalar.Style is not (ScalarStyle.Plain or ScalarStyle.Any))
            return JsonValue.Create(text);

        if (_null.IsMatch(text))
            return null;

        if (_bool.IsMatch(text))
            return JsonValue.Create(text.ToLowerInvariant() == "true");

        if (_int.IsMatch(text))
            return JsonValue.Create(long.Parse(text, CultureInfo.InvariantCulture));

        if (_float.IsMatch(text))
            return JsonValue.Create(double.Parse(text, CultureInfo.InvariantCulture));

        return JsonValue.Create(text);
    }
}
