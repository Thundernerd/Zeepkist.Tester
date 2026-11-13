using System.IO;
using TNRD.Zeepkist.Tester.Cli;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class ReferenceFoldersTests
{
    [Fact]
    public void For_PluginWithItsAssetsFile_TakesTheVersionItBuiltAgainst()
    {
        using var folder = new TemporaryFolder();
        var cache = CacheWith(folder, "zeepkist.gamelibs", "17.32.1912", "18.10.2087");
        var plugin = PluginBuiltWith(folder, cache, "Zeepkist.GameLibs/17.32.1912", "zeepkist.gamelibs/17.32.1912");

        var folders = ReferenceFolders.For([plugin]);

        Assert.Contains(Path.Combine(cache, "zeepkist.gamelibs", "17.32.1912", "lib"), folders);
        Assert.DoesNotContain(Path.Combine(cache, "zeepkist.gamelibs", "18.10.2087", "lib"), folders);
    }

    private static string CacheWith(TemporaryFolder folder, string package, params string[] versions)
    {
        var cache = Path.Combine(folder.Path, "cache");
        foreach (var version in versions)
            Directory.CreateDirectory(Path.Combine(cache, package, version, "lib"));

        return cache;
    }

    private static string PluginBuiltWith(TemporaryFolder folder, string cache, string library, string libraryPath)
    {
        var project = Path.Combine(folder.Path, "MyMod");
        var output = Directory.CreateDirectory(Path.Combine(project, "bin", "Debug", "net472")).FullName;
        var plugin = Path.Combine(output, "MyMod.dll");
        File.WriteAllBytes(plugin, []);

        Directory.CreateDirectory(Path.Combine(project, "obj"));
        File.WriteAllText(Path.Combine(project, "obj", "project.assets.json"), $$"""
            {
              "version": 3,
              "libraries": {
                "{{library}}": { "type": "package", "path": "{{libraryPath}}" }
              },
              "packageFolders": {
                {{System.Text.Json.JsonSerializer.Serialize(cache + Path.DirectorySeparatorChar)}}: {}
              }
            }
            """);
        return plugin;
    }
}
