using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// What a command works with: the nearest <c>zeepkist-test.json</c>, and the steps of the tester and the test plugins.
/// </summary>
internal sealed class ProjectSteps
{
    private ProjectSteps(TestConfig config, StepRegistry registry)
    {
        Config = config;
        Registry = registry;
    }

    /// <summary>
    /// Null when there's none.
    /// </summary>
    public TestConfig Config { get; }

    public StepRegistry Registry { get; }

    /// <summary>
    /// Reads the config in <paramref name="folder"/> or above it, and the steps of <paramref name="plugins"/>, or of the
    /// config's <c>testPlugin</c> when none are given.
    /// </summary>
    /// <returns>Null when either can't be read, after writing why to <paramref name="output"/>.</returns>
    public static ProjectSteps Read(string folder, IReadOnlyList<string> plugins, TextWriter output)
    {
        TestConfig config;
        try
        {
            config = TestConfig.Find(folder);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return Fail(output, $"Can't read {TestConfig.FileName}: {exception.Message}");
        }

        var paths = new List<string>(plugins);
        if (paths.Count == 0 && config?.TestPlugin != null)
        {
            var plugin = config.FindTestPluginDll();
            if (plugin == null)
                return Fail(output, $"Build {config.TestPlugin} first, or pass --plugin.");

            paths.Add(plugin);
        }

        try
        {
            return new ProjectSteps(config, OfflineRegistry.Read(paths));
        }
        catch (Exception exception) when (exception is StepDefinitionException or UnreadableAssemblyException)
        {
            return Fail(output, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or TypeLoadException)
        {
            return Fail(output, $"Can't read the plugins: {exception.Message}");
        }
    }

    private static ProjectSteps Fail(TextWriter output, string problem)
    {
        output.WriteLine($"error: {problem}");
        return null;
    }
}
