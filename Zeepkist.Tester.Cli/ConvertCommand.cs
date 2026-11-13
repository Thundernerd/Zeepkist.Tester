using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// <c>ztest convert</c>: writes each <c>.zlstest</c> file as a <c>.ztest.yaml</c> next to it. Exits with 1 on errors.
/// </summary>
internal sealed class ConvertCommand
{
    public const string Usage = "  convert <files or folders...> [--plugin <dll>]...   Writes each .zlstest file as a .ztest.yaml next to it";

    private const string OldExtension = ".zlstest";
    private const string NewExtension = ".ztest.yaml";

    private readonly TextWriter _output;
    private readonly List<string> _paths = [];
    private readonly List<string> _plugins = [];

    private ConvertCommand(TextWriter output)
    {
        _output = output;
    }

    /// <param name="arguments">What came after <c>convert</c>.</param>
    /// <param name="output">Where to report.</param>
    /// <param name="folder">Where to look for the config.</param>
    /// <returns>The exit code, or null when the arguments aren't understood.</returns>
    public static int? Run(IReadOnlyList<string> arguments, TextWriter output, string folder)
    {
        var command = new ConvertCommand(output);
        return command.ReadArguments(arguments) ? command.Run(folder) : null;
    }

    private bool ReadArguments(IReadOnlyList<string> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] == "--plugin" && i + 1 < arguments.Count)
                _plugins.Add(arguments[++i]);
            else if (arguments[i].StartsWith('-'))
                return false;
            else
                _paths.Add(arguments[i]);
        }

        return _paths.Count > 0;
    }

    private int Run(string folder)
    {
        var steps = PluginSteps.Read(_plugins, folder);
        if (steps.Error != null)
            return Fail(steps.Error);

        var files = new List<string>();
        foreach (var path in _paths)
        {
            if (Directory.Exists(path))
                files.AddRange(Directory.GetFiles(path, "*" + OldExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            else
                files.Add(path);
        }

        var errors = 0;
        var converted = 0;
        foreach (var file in files)
        {
            if (Convert(file, steps.Registry))
                converted++;
            else
                errors++;
        }

        _output.WriteLine($"Converted {Counted(converted, "file")}: {Counted(errors, "error")}.");
        return errors == 0 ? 0 : 1;
    }

    private bool Convert(string file, StepRegistry registry)
    {
        var target = Target(file);
        if (File.Exists(target))
        {
            _output.WriteLine($"error: {target} is already there. Move it out of the way to convert {file}.");
            return false;
        }

        try
        {
            LineScenario scenario;
            using (var reader = File.OpenText(file))
                scenario = LineScenarioReader.Read(file, reader, registry.Steps);

            File.WriteAllText(target, LineScenarioConverter.Write(scenario, registry.Steps));
        }
        catch (ScenarioException exception)
        {
            _output.WriteLine($"{exception.Location}: error: {exception.Problem}");
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _output.WriteLine($"error: Can't convert {file}: {exception.Message}");
            return false;
        }

        _output.WriteLine($"Wrote {target}");
        return true;
    }

    private static string Target(string file)
    {
        var name = Path.GetFileName(file);
        var stem = name.EndsWith(OldExtension, StringComparison.Ordinal) ? name.Substring(0, name.Length - OldExtension.Length) : Path.GetFileNameWithoutExtension(name);
        return Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, stem + NewExtension);
    }

    private int Fail(string problem)
    {
        _output.WriteLine($"error: {problem}");
        return 1;
    }

    private static string Counted(int count, string what)
    {
        var number = count == 0 ? "no" : count.ToString(CultureInfo.InvariantCulture);
        return $"{number} {what}{(count == 1 ? string.Empty : "s")}";
    }
}
