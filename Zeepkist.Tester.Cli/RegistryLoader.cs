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
    public static LoadedRegistry Load(IReadOnlyList<string> plugins, string folder)
    {
        TestConfig config;
        try
        {
            config = TestConfig.Find(folder);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new LoadedRegistry(null, null, $"Can't read {TestConfig.FileName}: {exception.Message}");
        }

        var paths = new List<string>(plugins);
        if (paths.Count == 0 && config?.TestPlugin != null)
        {
            var plugin = config.FindTestPluginDll();
            if (plugin == null)
                return new LoadedRegistry(config, null, $"Build {config.TestPlugin} first, or pass --plugin.");

            paths.Add(plugin);
        }

        try
        {
            return new LoadedRegistry(config, OfflineRegistry.Read(paths), null);
        }
        catch (StepDefinitionException exception)
        {
            return new LoadedRegistry(config, null, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or TypeLoadException)
        {
            return new LoadedRegistry(config, null, $"Can't read the plugins: {exception.Message}");
        }
    }
}
