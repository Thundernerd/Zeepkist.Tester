using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The rules a <see cref="ValueKindAttribute"/> follows, and what breaking them fails with: when steps are built, and in the game
/// for a kind no argument reaches.
/// </summary>
internal static class ValueKindRules
{
    /// <summary>
    /// Whether <paramref name="name"/> is a public string property of <paramref name="type"/> with a public getter.
    /// </summary>
    /// <remarks>
    /// Compares by name, so it also works on types from a MetadataLoadContext.
    /// </remarks>
    public static bool IsSubstituteProperty(Type type, string name)
    {
        var property = type.GetProperty(name);
        return property?.GetGetMethod() != null && property.PropertyType.FullName == typeof(string).FullName;
    }

    public static string Clash(Type existing, Type clashing, string kind)
    {
        return $"{existing.FullName} and {clashing.FullName} are both the value kind '{kind}'. A kind's name has to be unique across every mod's steps.";
    }

    public static string SubstituteNotAString(string member, Type valueType)
    {
        return $"Value kind Substitute '{member}' ({valueType.FullName}) isn't a public string property of it.";
    }
}
