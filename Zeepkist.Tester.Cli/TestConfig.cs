using System.IO;
using System.Linq;
using System.Text.Json;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// What a mod's repo says about its tests in <c>zeepkist-test.json</c>, as far as the offline tools need it.
/// </summary>
internal sealed class TestConfig
{
    public const string FileName = "zeepkist-test.json";

    private static readonly JsonDocumentOptions _options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private TestConfig(string folder, string scenarios, string testPlugin)
    {
        Folder = folder;
        Scenarios = scenarios;
        TestPlugin = testPlugin;
    }

    /// <summary>
    /// The folder the file is in, which the paths in it are relative to.
    /// </summary>
    public string Folder { get; }

    public string Scenarios { get; }

    /// <summary>
    /// The test plugin's project or its built DLL.
    /// </summary>
    public string TestPlugin { get; }

    /// <summary>
    /// Reads the file in <paramref name="folder"/> or the nearest folder above it. Null when there's none.
    /// </summary>
    /// <exception cref="JsonException">The file isn't valid JSON.</exception>
    public static TestConfig Find(string folder)
    {
        for (var current = new DirectoryInfo(folder); current != null; current = current.Parent)
        {
            var path = Path.Combine(current.FullName, FileName);
            if (!File.Exists(path))
                continue;

            using var document = JsonDocument.Parse(File.ReadAllText(path), _options);
            return new TestConfig(current.FullName, Text(document.RootElement, "scenarios"), Text(document.RootElement, "testPlugin"));
        }

        return null;
    }

    /// <summary>
    /// The built DLL of <see cref="TestPlugin"/>: for a project, the newest under its <c>bin</c> folder. Null when it isn't built.
    /// </summary>
    public string FindTestPluginDll()
    {
        var path = Path.Combine(Folder, TestPlugin);
        if (Path.GetExtension(path) == ".dll")
            return path;

        var bin = Path.Combine(Path.GetDirectoryName(path), "bin");
        if (!Directory.Exists(bin))
            return null;

        return new DirectoryInfo(bin)
            .EnumerateFiles(Path.GetFileNameWithoutExtension(path) + ".dll", SearchOption.AllDirectories)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private static string Text(JsonElement root, string name)
    {
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
