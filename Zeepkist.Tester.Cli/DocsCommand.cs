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
            if (PluginArguments.TryRead(arguments, ref i, plugins))
                continue;

            if (arguments[i] == "--output" && i + 1 < arguments.Count && file == null)
                file = arguments[++i];
            else
                return null;
        }

        var steps = ProjectSteps.Read(folder, plugins, output);
        if (steps == null)
            return 1;

        var path = Path.Combine(folder, file ?? DefaultOutput);
        try
        {
            File.WriteAllText(path, StepReference.Write(steps.Registry));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return output.Fail($"Can't write {path}: {exception.Message}");
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Wrote {steps.Registry.Steps.Count} steps and {steps.Registry.States.Count} states to {path}."));
        return 0;
    }
}
