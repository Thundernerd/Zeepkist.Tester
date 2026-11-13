using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var steps = PluginSteps.Read(_plugins, folder);
        if (steps.Error != null)
            return Fail(steps.Error);

        return _checkRegistry ? CheckRegistry(steps.Registry) : CheckScenarios(steps.Registry, steps.Config);
    }

    private int CheckRegistry(StepRegistry registry)
    {
        var smells = RegistryCheck.Smells(registry);
        foreach (var smell in smells)
            _output.WriteLine($"warning: {smell}");

        _output.WriteLine($"Checked {Counted.Of(registry.Steps.Count, "step")} and {Counted.Of(registry.States.Count, "state")}: {Counted.Of(smells.Count, "warning", "no")}.");
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
        _output.WriteLine($"Checked {Counted.Of(files.Count, "file")}: {Counted.Of(errors, "error", "no")}, {Counted.Of(problems.Count - errors, "warning", "no")}.");
        return errors == 0 ? 0 : 1;
    }

    private int Fail(string problem)
    {
        _output.WriteLine($"error: {problem}");
        return 1;
    }
}
