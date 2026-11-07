using System.Text.RegularExpressions;

namespace TNRD.Zeepkist.Tester;

internal static class ScreenshotNames
{
    /// <summary>
    /// Each scenario's screenshots are saved in one folder, so its name keeps them apart. A file name is what's left of it
    /// a <see cref="Sdk.FileName"/> allows.
    /// </summary>
    public static string For(string scenario, string name)
    {
        return $"{Regex.Replace(scenario, "[^A-Za-z0-9_-]", "-")}-{name}";
    }
}
