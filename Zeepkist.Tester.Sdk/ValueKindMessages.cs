using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// What a broken <see cref="ValueKindAttribute"/> fails with: when steps are built, and in the game for a kind no argument reaches.
/// </summary>
internal static class ValueKindMessages
{
    public static string Clash(Type one, Type other, string kind)
    {
        return $"{one.FullName} and {other.FullName} are both the value kind '{kind}'. A kind's name has to be unique across every mod's steps.";
    }

    public static string SubstituteNotAString(string member, Type valueType)
    {
        return $"Value kind Substitute '{member}' ({valueType.FullName}) isn't a public string property of it.";
    }
}
