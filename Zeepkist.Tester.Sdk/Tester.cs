using System.Collections.Generic;
using BepInEx;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Where a mod's test plugin gives the harness what its steps run against.
/// </summary>
public static class Tester
{
    private static readonly List<TestPluginRegistration> _registered = [];

    internal static IReadOnlyList<TestPluginRegistration> Registered => _registered;

    /// <summary>
    /// Call this from the plugin's <c>Awake</c>. The steps themselves are found by scanning <see cref="TestPluginAttribute"/> assemblies.
    /// </summary>
    public static TestPluginRegistration Register(BaseUnityPlugin plugin)
    {
        return Register(plugin.GetType().FullName);
    }

    internal static TestPluginRegistration Register(string pluginName)
    {
        var registration = new TestPluginRegistration(pluginName);
        _registered.Add(registration);
        return registration;
    }
}
