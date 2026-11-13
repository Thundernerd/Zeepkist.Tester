using System;
using System.Collections.Generic;
using System.IO;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// <c>ztest schema</c>: writes <c>.ztest/schema.json</c> next to the nearest <c>zeepkist-test.json</c>, or in the
/// current folder when there's none.
/// </summary>
internal static class SchemaCommand
{
    public const string Usage = "  schema [--plugin <dll>]...                        Writes .ztest/schema.json, for editors to check and complete scenarios with";

    /// <param name="arguments">What came after <c>schema</c>.</param>
    /// <param name="output">Where to report.</param>
    /// <param name="folder">Where to look for the config.</param>
    /// <returns>The exit code, or null when the arguments aren't understood.</returns>
    public static int? Run(IReadOnlyList<string> arguments, TextWriter output, string folder)
    {
        var plugins = new List<string>();
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] != "--plugin" || i + 1 >= arguments.Count)
                return null;

            plugins.Add(arguments[++i]);
        }

        var steps = ProjectSteps.Read(folder, plugins, output);
        if (steps == null)
            return 1;

        try
        {
            output.WriteLine($"Wrote {SchemaFile.Write(steps.Config?.Folder ?? folder, steps.Registry)}.");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"error: Can't write {SchemaFile.RelativePath}: {exception.Message}");
            return 1;
        }
    }
}
