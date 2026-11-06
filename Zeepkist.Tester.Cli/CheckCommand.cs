using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// <c>ztest check</c>: checks scenarios, or with <c>--registry</c> the steps and states themselves, with no game. Exits with 1 on errors.
/// </summary>
internal sealed class CheckCommand
{
    public const string Usage = "  check [files or folders...] [--plugin <dll>]...   Checks scenarios offline, against the tester's steps and the plugins'\n"
        + "  check --registry [--plugin <dll>]...              Lists states no step enters, and other registry smells";

    private const string ScenarioPattern = "*.ztest.yaml";

    private readonly TextWriter _output;
    private readonly List<string> _paths = [];
    private readonly List<string> _plugins = [];
    private bool _checkRegistry;

    private CheckCommand(TextWriter output)
    {
        _output = output;
    }

    /// <param name="arguments">What came after <c>check</c>.</param>
    /// <param name="output">Where to report.</param>
    /// <param name="folder">Where to look for the config.</param>
    /// <returns>The exit code, or null when the arguments aren't understood.</returns>
    public static int? Run(IReadOnlyList<string> arguments, TextWriter output, string folder)
    {
        var command = new CheckCommand(output);
        return command.ReadArguments(arguments) ? command.Run(folder) : null;
    }

    private bool ReadArguments(IReadOnlyList<string> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] == "--registry")
            {
                _checkRegistry = true;
            }
            else if (arguments[i] == "--plugin" && i + 1 < arguments.Count)
            {
                _plugins.Add(arguments[++i]);
            }
            else if (arguments[i].StartsWith('-'))
            {
                return false;
            }
            else
            {
                _paths.Add(arguments[i]);
            }
        }

        return true;
    }

    private int Run(string folder)
    {
        TestConfig config;
        try
        {
            config = TestConfig.Find(folder);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return Fail($"Can't read {TestConfig.FileName}: {exception.Message}");
        }

        if (_plugins.Count == 0 && config?.TestPlugin != null)
        {
            var plugin = config.FindTestPluginDll();
            if (plugin == null)
                return Fail($"Build {config.TestPlugin} first, or pass --plugin.");

            _plugins.Add(plugin);
        }

        StepRegistry registry;
        try
        {
            registry = OfflineRegistry.Read(_plugins);
        }
        catch (StepDefinitionException exception)
        {
            return Fail(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or TypeLoadException)
        {
            return Fail($"Can't read the plugins: {exception.Message}");
        }

        return _checkRegistry ? CheckRegistry(registry) : CheckScenarios(registry, config);
    }

    private int CheckRegistry(StepRegistry registry)
    {
        var smells = RegistryCheck.Smells(registry);
        foreach (var smell in smells)
            _output.WriteLine($"warning: {smell}");

        _output.WriteLine($"Checked {Counted(registry.Steps.Count, "step")} and {Counted(registry.States.Count, "state")}: {Counted(smells.Count, "warning", "no")}.");
        return 0;
    }

    private int CheckScenarios(StepRegistry registry, TestConfig config)
    {
        var paths = _paths;
        if (paths.Count == 0)
        {
            if (config?.Scenarios == null)
                return Fail($"No files given, and no {TestConfig.FileName} with \"scenarios\" here or above.");

            paths = [Path.GetRelativePath(Environment.CurrentDirectory, Path.Combine(config.Folder, config.Scenarios))];
        }

        var files = new List<string>();
        foreach (var path in paths)
        {
            if (!Directory.Exists(path))
            {
                files.Add(path);
                continue;
            }

            var inFolder = Directory.GetFiles(path, ScenarioPattern, SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
            if (inFolder.Count == 0)
                return Fail($"No {ScenarioPattern.TrimStart('*')} files in {path}.");

            files.AddRange(inFolder);
        }

        var problems = ScenarioCheck.Run(files, registry);
        foreach (var problem in problems)
            _output.WriteLine(problem);

        var errors = problems.Count(problem => problem.Severity == CheckSeverity.Error);
        _output.WriteLine($"Checked {Counted(files.Count, "file")}: {Counted(errors, "error", "no")}, {Counted(problems.Count - errors, "warning", "no")}.");
        return errors == 0 ? 0 : 1;
    }

    private int Fail(string problem)
    {
        _output.WriteLine($"error: {problem}");
        return 1;
    }

    private static string Counted(int count, string what, string none = null)
    {
        var number = count == 0 && none != null ? none : count.ToString(CultureInfo.InvariantCulture);
        return $"{number} {what}{(count == 1 ? string.Empty : "s")}";
    }
}
