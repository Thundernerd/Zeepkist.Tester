using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// The registry a command reads scenarios against: the tester's steps and those of the plugins it was given, or else of
/// the nearest <c>zeepkist-test.json</c>'s <c>testPlugin</c>.
/// </summary>
internal sealed class PluginSteps
{
    private PluginSteps(TestConfig config, StepRegistry registry, string error)
    {
        Config = config;
        Registry = registry;
        Error = error;
    }

    /// <summary>
    /// Null when there's no config.
    /// </summary>
    public TestConfig Config { get; }

    /// <summary>
    /// Null when <see cref="Error"/> says why it can't be read.
    /// </summary>
    public StepRegistry Registry { get; }

    public string Error { get; }

    /// <param name="plugins">What <c>--plugin</c> gave.</param>
    /// <param name="folder">Where to look for the config.</param>
    public static PluginSteps Read(IReadOnlyList<string> plugins, string folder)
    {
        TestConfig config;
        try
        {
            config = TestConfig.Find(folder);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return Failed(null, $"Can't read {TestConfig.FileName}: {exception.Message}");
        }

        var paths = new List<string>(plugins);
        if (paths.Count == 0 && config?.TestPlugin != null)
        {
            var plugin = config.FindTestPluginDll();
            if (plugin == null)
                return Failed(config, $"Build {config.TestPlugin} first, or pass --plugin.");

            paths.Add(plugin);
        }

        try
        {
            return new PluginSteps(config, OfflineRegistry.Read(paths), null);
        }
        catch (StepDefinitionException exception)
        {
            return Failed(config, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or TypeLoadException)
        {
            return Failed(config, $"Can't read the plugins: {exception.Message}");
        }
    }

    private static PluginSteps Failed(TestConfig config, string error)
    {
        return new PluginSteps(config, null, error);
    }
}
