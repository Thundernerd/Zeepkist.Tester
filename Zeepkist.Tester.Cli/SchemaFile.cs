using System;
using System.IO;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// <c>.ztest/schema.json</c>, which editors check and complete scenarios with.
/// </summary>
internal static class SchemaFile
{
    /// <summary>
    /// Where it is from the folder of <c>zeepkist-test.json</c>, as <c>yaml.schemas</c> and modelines name it.
    /// </summary>
    public const string RelativePath = ".ztest/schema.json";

    /// <returns>The file's path.</returns>
    /// <exception cref="IOException">It can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">It can't be written.</exception>
    public static string Write(string folder, StepRegistry registry)
    {
        var path = Path.GetFullPath(Path.Combine(folder, RelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, StepSchema.Write(registry.Steps) + "\n");
        return path;
    }

    /// <summary>
    /// Writes it on the side of another command, which a file that can't be written doesn't fail.
    /// </summary>
    public static void WriteQuietly(string folder, StepRegistry registry, TextWriter output)
    {
        try
        {
            Write(folder, registry);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"warning: Can't write {RelativePath}: {exception.Message}");
        }
    }
}
