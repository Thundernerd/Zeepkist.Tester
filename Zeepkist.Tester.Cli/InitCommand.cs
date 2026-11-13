using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// <c>ztest init</c>: has VS Code's YAML extension check scenarios against <c>.ztest/schema.json</c>, with a
/// <c>yaml.schemas</c> entry in <c>.vscode/settings.json</c> next to the nearest <c>zeepkist-test.json</c>, or in the
/// current folder when there's none.
/// </summary>
internal static class InitCommand
{
    public const string Usage = "  init                                              Has VS Code check scenarios against the schema, in .vscode/settings.json";

    private const string Setting = "yaml.schemas";
    private const string ScenarioGlob = "**/*.ztest.yaml";

    private static readonly JsonDocumentOptions _readOptions = new JsonDocumentOptions { AllowTrailingCommas = true };

    private static readonly JsonSerializerOptions _writeOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="arguments">What came after <c>init</c>.</param>
    /// <param name="output">Where to report.</param>
    /// <param name="folder">Where to look for the config.</param>
    /// <returns>The exit code, or null when the arguments aren't understood.</returns>
    public static int? Run(IReadOnlyList<string> arguments, TextWriter output, string folder)
    {
        if (arguments.Count > 0)
            return null;

        try
        {
            var root = TestConfig.Find(folder)?.Folder ?? folder;
            var exitCode = AddSchemaSetting(Path.Combine(root, ".vscode", "settings.json"), output);
            if (exitCode == 0)
                WriteOtherEditors(output);

            return exitCode;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static int AddSchemaSetting(string path, TextWriter output)
    {
        var settings = new JsonObject();
        if (File.Exists(path))
        {
            var text = File.ReadAllText(path);
            if (HasComments(text))
            {
                output.WriteLine($"error: {path} has comments, which rewriting it would lose. Add this to it yourself:");
                output.WriteLine($"    \"{Setting}\": {{ \"{SchemaFile.RelativePath}\": \"{ScenarioGlob}\" }}");
                return 1;
            }

            if (JsonNode.Parse(text, documentOptions: _readOptions) is not JsonObject read)
                return Fail(output, $"{path} isn't a JSON object.");

            settings = read;
        }

        if (settings[Setting] == null)
            settings[Setting] = new JsonObject();

        if (settings[Setting] is not JsonObject schemas)
            return Fail(output, $"{Setting} in {path} isn't a JSON object.");

        if (!AddGlob(schemas))
        {
            output.WriteLine($"{path} already has scenarios checked against {SchemaFile.RelativePath}.");
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, settings.ToJsonString(_writeOptions) + "\n");
        output.WriteLine($"Wrote {path}: VS Code with Red Hat's YAML extension checks {ScenarioGlob} against {SchemaFile.RelativePath}.");
        return 0;
    }

    /// <summary>
    /// Has the schema's entry take scenario files, beside any other files it takes.
    /// </summary>
    /// <returns>Whether it changed.</returns>
    private static bool AddGlob(JsonObject schemas)
    {
        var globs = schemas[SchemaFile.RelativePath] switch
        {
            null => [],
            JsonArray list => list.Select(glob => glob?.ToString()).ToList(),
            var one => [one.ToString()],
        };

        if (globs.Contains(ScenarioGlob))
            return false;

        schemas[SchemaFile.RelativePath] = globs.Count == 0
            ? ScenarioGlob
            : new JsonArray([.. globs.Append(ScenarioGlob).Select(glob => (JsonNode)glob)]);
        return true;
    }

    private static bool HasComments(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.Comment)
                    return true;
            }
        }
        catch (JsonException)
        {
            // Not JSON at all, which reading it says
        }

        return false;
    }

    private static void WriteOtherEditors(TextWriter output)
    {
        output.WriteLine("Run ztest schema, or ztest check, to write the schema. In other editors, or for a scenario outside this folder, start the file with a comment that points at it:");
        output.WriteLine($"    # yaml-language-server: $schema=<path to {SchemaFile.RelativePath}>   (VS Code)");
        output.WriteLine($"    # $schema: <path to {SchemaFile.RelativePath}>                        (Rider)");
    }

    private static int Fail(TextWriter output, string problem)
    {
        output.WriteLine($"error: {problem}");
        return 1;
    }
}
