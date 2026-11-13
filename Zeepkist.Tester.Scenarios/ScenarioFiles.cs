using System;
using System.Collections.Generic;
using System.IO;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Reads a scenario file in whichever format its name says: <c>.ztest.yaml</c>, or for one release ZeepLogicScript's old <c>.zlstest</c>.
/// </summary>
internal static class ScenarioFiles
{
    public const string Extension = ".ztest.yaml";

    public const string OldExtension = ".zlstest";

    /// <summary>
    /// Both formats' extensions, the current one first.
    /// </summary>
    public static readonly IReadOnlyList<string> Extensions = [Extension, OldExtension];

    /// <summary>
    /// Whether it's a <c>.zlstest</c>, read line by line.
    /// </summary>
    public static bool IsOld(string file)
    {
        return file.EndsWith(OldExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The name without its scenario extension, of either format.
    /// </summary>
    public static string WithoutExtension(string name)
    {
        foreach (var extension in Extensions)
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - extension.Length);
        }

        return name;
    }

    /// <exception cref="ScenarioException">The file can't be read as a scenario. This is the first problem in it.</exception>
    public static Scenario Read(string file, TextReader reader, IEnumerable<StepDescriptor> steps)
    {
        return IsOld(file) ? LineScenarioReader.Read(file, reader, steps).ToScenario(file) : ScenarioParser.Parse(file, reader, steps);
    }

    /// <summary>
    /// As <see cref="ScenarioParser.ParseAll"/>. A <c>.zlstest</c> stops at its first problem, as the old format did.
    /// </summary>
    public static ScenarioReading ReadAll(string file, TextReader reader, IEnumerable<StepDescriptor> steps)
    {
        if (!IsOld(file))
            return ScenarioParser.ParseAll(file, reader, steps);

        try
        {
            return new ScenarioReading(LineScenarioReader.Read(file, reader, steps).ToScenario(file), []);
        }
        catch (ScenarioException exception)
        {
            return new ScenarioReading(null, [exception]);
        }
    }
}
