using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Makes sure each mod under test that the game loaded is the build the runner deployed, in a folder named after its
/// GUID next to this plugin's, and that BepInEx didn't find another copy of it.
/// </summary>
internal static class BuildCheck
{
    /// <summary>
    /// A step for each of the request's mods.
    /// </summary>
    public static List<RunStep> Run(RunRequest request, string testerFolder)
    {
        var pluginsFolder = Path.GetDirectoryName(testerFolder);
        var loaded = Chainloader.PluginInfos.Values
            .Select(plugin => new LoadedPlugin(plugin.Metadata.GUID, plugin.Metadata.Name, plugin.Location))
            .ToList();
        var log = ReadDiskLog();
        return request.Mods.ConvertAll(mod =>
        {
            var name = $"right build: {mod.Guid}";
            var problem = Problem(mod, pluginsFolder, loaded, log);
            return problem == null ? RunStep.Pass(name) : RunStep.Fail(name, problem);
        });
    }

    /// <summary>
    /// What's wrong with the build of <paramref name="mod"/>, or null.
    /// </summary>
    public static string Problem(ModUnderTest mod, string pluginsFolder, IReadOnlyCollection<LoadedPlugin> loaded, IReadOnlyCollection<string> log)
    {
        var plugin = loaded.FirstOrDefault(other => other.Guid == mod.Guid);
        if (plugin == null)
            return $"{mod.Guid} isn't loaded";

        var expected = Path.GetFullPath(Path.Combine(pluginsFolder, mod.Guid));
        var actual = Path.GetFullPath(Path.GetDirectoryName(plugin.Location));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            return $"{mod.Guid} loaded from {actual}, not the deployed build in {expected}";

        foreach (var other in loaded)
        {
            if (other.Guid != mod.Guid && (other.Name == plugin.Name || mod.OldGuids.Contains(other.Guid)))
                return $"Another copy of {mod.Guid} loaded too: {other.Location}";
        }

        var skipped = SkippedCopy(plugin.Name, log);
        return skipped == null ? null : $"BepInEx found another copy: {skipped}";
    }

    /// <summary>
    /// BepInEx only logs the copies it skips for an older version of the same plugin, as its name and version.
    /// </summary>
    private static string SkippedCopy(string name, IReadOnlyCollection<string> log)
    {
        var skipping = new Regex($@"Skipping \[{Regex.Escape(name)} [^\s\]]+\].*");
        return log.Select(line => skipping.Match(line)).FirstOrDefault(match => match.Success)?.Value;
    }

    /// <summary>
    /// BepInEx's log file is still open.
    /// </summary>
    private static List<string> ReadDiskLog()
    {
        List<string> lines = [];
        var path = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
        if (!File.Exists(path))
            return lines;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        string line;
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);

        return lines;
    }
}
