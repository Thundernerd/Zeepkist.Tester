using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
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
            if (PluginArguments.TryRead(arguments, ref i, _plugins))
                continue;

            if (arguments[i] == "--registry")
            {
                _checkRegistry = true;
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
        var steps = ProjectSteps.Read(folder, _plugins, _output);
        if (steps == null)
            return 1;

        if (steps.Config != null)
            SchemaFile.WriteQuietly(steps.Config.Folder, steps.Registry, _output);

        return _checkRegistry ? CheckRegistry(steps.Registry) : CheckScenarios(steps.Registry, steps.Config);
    }

    private int CheckRegistry(StepRegistry registry)
    {
        var smells = RegistryCheck.Smells(registry);
        foreach (var smell in smells)
            _output.WriteWarning(smell);

        _output.WriteLine($"Checked {Counted.Of(registry.Steps.Count, "step")} and {Counted.Of(registry.States.Count, "state")}: {Counted.Of(smells.Count, "warning", "no")}.");
        return 0;
    }

    private int CheckScenarios(StepRegistry registry, TestConfig config)
    {
        var paths = _paths;
        if (paths.Count == 0)
        {
            if (config?.Scenarios == null)
                return _output.Fail($"No files given, and no {TestConfig.FileName} with \"scenarios\" here or above.");

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
                return _output.Fail($"No {ScenarioPattern.TrimStart('*')} files in {path}.");

            files.AddRange(inFolder);
        }

        var problems = ScenarioCheck.Run(files, registry);
        foreach (var problem in problems)
            _output.WriteLine(problem);

        var testErrors = MissingTestLevels(registry, config);
        foreach (var error in testErrors)
            _output.WriteError(error);

        var warnings = problems.Count(problem => problem.Severity == CheckSeverity.Warning);
        var errors = problems.Count - warnings + testErrors.Count;
        _output.WriteLine($"Checked {Counted.Of(files.Count, "file")}: {Counted.Of(errors, "error", "no")}, {Counted.Of(warnings, "warning", "no")}.");
        return errors == 0 ? 0 : 1;
    }

    /// <summary>
    /// Each C# test whose <see cref="TestDescriptor.Level"/> isn't in the configured <c>scenarios</c> folder, which is where the
    /// game looks for it. Without one there's nowhere to look.
    /// </summary>
    private static List<string> MissingTestLevels(StepRegistry registry, TestConfig config)
    {
        if (config?.Scenarios == null)
            return [];

        var folder = Path.Combine(config.Folder, config.Scenarios);
        return registry.Tests
            .Where(test => test.Level != null && !File.Exists(ScenarioPaths.InFolder(folder, test.Level)))
            .Select(test => $"[InGameTest] {test.TypeName}.{test.MethodName} can't find its Level '{test.Level}' in {folder}.")
            .ToList();
    }
}
