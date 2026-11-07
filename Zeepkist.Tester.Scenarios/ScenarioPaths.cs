using System.IO;

namespace TNRD.Zeepkist.Tester.Scenarios;

internal static class ScenarioPaths
{
    /// <summary>
    /// A file a scenario names, which it writes relative to its own file with forward slashes on every platform.
    /// </summary>
    public static string FromScenario(string scenarioFile, string written)
    {
        return Path.Combine(Path.GetDirectoryName(scenarioFile), written.Replace('/', Path.DirectorySeparatorChar));
    }
}
