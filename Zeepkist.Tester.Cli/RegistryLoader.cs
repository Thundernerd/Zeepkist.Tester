using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// The registry a command works with: the tester's steps and those of the plugins it's given, or else of the nearest config's test plugin.
/// </summary>
internal static class RegistryLoader
{
    /// <param name="plugins">The plugins from <c>--plugin</c>.</param>
    /// <param name="folder">Where to look for the config.</param>
    /// <param name="config">The nearest config, or null when there's none.</param>
    /// <param name="registry">The registry, or null on a problem.</param>
    /// <returns>Why there's no registry, or null.</returns>
    public static string Load(IReadOnlyList<string> plugins, string folder, out TestConfig config, out StepRegistry registry)
    {
        registry = null;
        try
        {
            config = TestConfig.Find(folder);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            config = null;
            return $"Can't read {TestConfig.FileName}: {exception.Message}";
        }

        var paths = new List<string>(plugins);
        if (paths.Count == 0 && config?.TestPlugin != null)
        {
            var plugin = config.FindTestPluginDll();
            if (plugin == null)
                return $"Build {config.TestPlugin} first, or pass --plugin.";

            paths.Add(plugin);
        }

        try
        {
            registry = OfflineRegistry.Read(paths);
            return null;
        }
        catch (StepDefinitionException exception)
        {
            return exception.Message;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or TypeLoadException)
        {
            return $"Can't read the plugins: {exception.Message}";
        }
    }
}
