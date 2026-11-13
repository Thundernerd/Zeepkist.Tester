using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// <c>ztest docs</c>: writes the step reference, the tester's steps and states and the plugins', as Markdown.
/// </summary>
internal static class DocsCommand
{
    public const string Usage = "  docs [--output <file>] [--plugin <dll>]...        Writes the step reference as Markdown, to steps.md unless --output says otherwise";

    private const string DefaultOutput = "steps.md";

    /// <param name="arguments">What came after <c>docs</c>.</param>
    /// <param name="output">Where to report.</param>
    /// <param name="folder">Where to look for the config, and to write to without <c>--output</c>.</param>
    /// <returns>The exit code, or null when the arguments aren't understood.</returns>
    public static int? Run(IReadOnlyList<string> arguments, TextWriter output, string folder)
    {
        var plugins = new List<string>();
        string file = null;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] == "--plugin" && i + 1 < arguments.Count)
                plugins.Add(arguments[++i]);
            else if (arguments[i] == "--output" && i + 1 < arguments.Count && file == null)
                file = arguments[++i];
            else
                return null;
        }

        var loaded = RegistryLoader.Load(plugins, folder);
        if (loaded.Problem != null)
            return Fail(output, loaded.Problem);

        var path = Path.Combine(folder, file ?? DefaultOutput);
        try
        {
            File.WriteAllText(path, StepReference.Write(loaded.Registry));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Fail(output, $"Can't write {path}: {exception.Message}");
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Wrote {loaded.Registry.Steps.Count} steps and {loaded.Registry.States.Count} states to {path}."));
        return 0;
    }

    private static int Fail(TextWriter output, string problem)
    {
        output.WriteLine($"error: {problem}");
        return 1;
    }
}
