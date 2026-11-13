using System;
using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Block types and their settings by names of this plugin's own, as a mod's manifest has them, such as ZeepLogicScript's Block
/// Functions and Settings.
/// </summary>
internal static class NamedBlocks
{
    private static readonly Dictionary<string, (int Type, Dictionary<string, string> Settings)> _blocks = new()
    {
        ["lamp"] = (2448, new Dictionary<string, string> { ["spotlight"] = "is" }),
        ["telemetry"] = (2440, new Dictionary<string, string> { ["mode"] = "xbs" }),
    };

    public static string Names => string.Join(", ", _blocks.Keys.OrderBy(name => name, StringComparer.Ordinal));

    public static bool TryGetType(string name, out int type)
    {
        var found = _blocks.TryGetValue(name, out var block);
        type = found ? block.Type : 0;
        return found;
    }

    /// <summary>
    /// The key a block of <paramref name="type"/> stores the setting called <paramref name="setting"/> under, or null.
    /// </summary>
    public static string KeyOf(int type, string setting)
    {
        var block = _blocks.Values.FirstOrDefault(candidate => candidate.Type == type);
        return block.Settings != null && block.Settings.TryGetValue(setting, out var key) ? key : null;
    }
}
