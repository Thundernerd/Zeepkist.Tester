using System.Globalization;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// How a command's summary line says how many there were.
/// </summary>
internal static class Counted
{
    /// <summary>
    /// Such as <c>1 file</c> or <c>2 files</c>, and <paramref name="none"/> for 0 when it's given, such as <c>no errors</c>.
    /// </summary>
    public static string Of(int count, string what, string none = null)
    {
        var number = count == 0 && none != null ? none : count.ToString(CultureInfo.InvariantCulture);
        return $"{number} {what}{(count == 1 ? string.Empty : "s")}";
    }
}
