using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The running game.
/// </summary>
internal sealed class GameHost : IRunHost
{
    private readonly Screenshots _screenshots;
    private readonly StartWalk _walk;

    public GameHost(ManualLogSource logger, GameEvents game, Screenshots screenshots, Action<RunStep> report)
    {
        Logger = logger;
        Game = game;
        _screenshots = screenshots;
        _walk = new StartWalk(game, logger, report);
    }

    public ManualLogSource Logger { get; }

    public float Now => Time.realtimeSinceStartup;

    public IGameEvents Game { get; }

    public LEV_LevelEditorCentral Editor
    {
        get
        {
            if (GameLocations.Current != GameLocation.Editor)
                return null;

            var central = ComponentCache.Get<LEV_LevelEditorCentral>();
            return central != null ? central : null;
        }
    }

    /// <summary>
    /// The step types of the tester and of each loaded plugin in a <see cref="TestPluginAttribute"/> assembly.
    /// </summary>
    public static IEnumerable<Type> FindStepTypes()
    {
        var plugins = Chainloader.PluginInfos.Values
            .Where(plugin => plugin.Instance != null)
            .Select(plugin => plugin.Instance.GetType().Assembly)
            .Where(CompiledAgainstTheSdk)
            .Where(StepDescriptorBuilder.IsTestPlugin);

        return plugins.Prepend(typeof(GameHost).Assembly).Distinct().SelectMany(StepDescriptorBuilder.FindSteps);
    }

    public IEnumerator EnterStart(ScenarioStart start, string scenarioFile)
    {
        return _walk.Enter(start, scenarioFile);
    }

    public IEnumerator Screenshot(string name)
    {
        return _screenshots.Take(name);
    }

    /// <summary>
    /// Only those can be test plugins, and listing an assembly's types throws when another installed mod is missing a dependency.
    /// </summary>
    private static bool CompiledAgainstTheSdk(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies().Any(name => name.Name == typeof(Step).Assembly.GetName().Name);
    }
}
