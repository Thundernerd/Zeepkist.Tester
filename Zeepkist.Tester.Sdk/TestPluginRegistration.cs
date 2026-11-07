using System;
using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// What one test plugin gave the harness.
/// </summary>
public sealed class TestPluginRegistration
{
    private readonly List<KeyValuePair<Type, object>> _services = [];
    private readonly List<IScenarioHooks> _hooks = [];

    internal TestPluginRegistration(string pluginName)
    {
        PluginName = pluginName;
    }

    internal string PluginName { get; }

    internal IReadOnlyList<KeyValuePair<Type, object>> Services => _services;

    internal IReadOnlyList<IScenarioHooks> ScenarioHooks => _hooks;

    /// <summary>
    /// Something steps and hooks get with <see cref="IRunContext.Get{T}"/>, under <typeparamref name="T"/>.
    /// </summary>
    public TestPluginRegistration Service<T>(T service)
        where T : class
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        _services.Add(new KeyValuePair<Type, object>(typeof(T), service));
        return this;
    }

    public TestPluginRegistration Hooks(IScenarioHooks hooks)
    {
        if (hooks == null)
            throw new ArgumentNullException(nameof(hooks));

        _hooks.Add(hooks);
        return this;
    }
}
