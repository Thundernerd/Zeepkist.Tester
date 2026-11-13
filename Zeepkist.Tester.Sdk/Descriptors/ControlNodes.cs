using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The nodes a scenario reads itself instead of as steps. No step can take one of these names, or it would be hidden
/// when it's written unprefixed.
/// </summary>
internal static class ControlNodes
{
    public const string For = "for";

    public const string Repeat = "repeat";

    public const string Run = "run";

    public static IReadOnlyList<string> All { get; } = [For, Repeat, Run];
}
