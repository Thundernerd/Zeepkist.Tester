using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// <c>--plugin &lt;dll&gt;</c>, which each command that reads steps takes as often as there are test plugins.
/// </summary>
internal static class PluginArguments
{
    private const string Option = "--plugin";

    /// <summary>
    /// Reads a <c>--plugin &lt;dll&gt;</c> at <paramref name="index"/> into <paramref name="plugins"/>, and moves
    /// <paramref name="index"/> onto the dll.
    /// </summary>
    /// <returns>Whether there was one. A <c>--plugin</c> with no dll after it isn't.</returns>
    public static bool TryRead(IReadOnlyList<string> arguments, ref int index, List<string> plugins)
    {
        if (arguments[index] != Option || index + 1 >= arguments.Count)
            return false;

        plugins.Add(arguments[++index]);
        return true;
    }
}
