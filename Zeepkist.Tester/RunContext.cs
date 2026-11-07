using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What the test plugins gave the harness, which is there for the whole run.
/// </summary>
internal class RunContext : IRunContext
{
    public RunContext(ManualLogSource logger, IReadOnlyDictionary<Type, object> services)
    {
        Logger = logger;
        Services = services;
    }

    public ManualLogSource Logger { get; }

    public IReadOnlyDictionary<Type, object> Services { get; }

    public T Get<T>()
        where T : class
    {
        return Services.TryGetValue(typeof(T), out var service)
            ? (T)service
            : throw new StepFailedException($"No test plugin gave a service of type {typeof(T).FullName}. A plugin gives one with Tester.Register(this).Service(...).");
    }
}
