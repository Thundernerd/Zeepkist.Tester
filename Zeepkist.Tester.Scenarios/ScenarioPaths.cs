using System.IO;

namespace TNRD.Zeepkist.Tester.Scenarios;

internal static class ScenarioPaths
{
    /// <summary>
    /// A file a scenario names, which it writes relative to its own file with forward slashes on every platform.
    /// </summary>
    public static string FromScenario(string scenarioFile, string written)
    {
        return InFolder(Path.GetDirectoryName(scenarioFile), written);
    }

    /// <summary>
    /// A file written relative to <paramref name="folder"/>, with forward slashes on every platform.
    /// </summary>
    public static string InFolder(string folder, string written)
    {
        return Path.Combine(folder, written.Replace('/', Path.DirectorySeparatorChar));
    }
}
