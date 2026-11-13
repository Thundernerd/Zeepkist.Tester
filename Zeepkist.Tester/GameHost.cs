using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Patches;
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
        Input_Legacy.Driver = new LegacyInputDriver(() => Time.frameCount);
        Action<Exception> logGiveBackFailure = exception => logger.LogError($"{TestRun.Marker} an input driver failed to give back: {exception}");
        var inProcess = new InProcessDrivers(logGiveBackFailure, new ImuiDriver(new ImuiBackend(), () => Time.unscaledTime), Input_Legacy.Driver, new UguiPointer<GameObject>(new UguiScene()));
        Input = new TesterInput(inProcess, new RewiredDriver(), () => Now, logGiveBackFailure);
    }

    public ManualLogSource Logger { get; }

    public float Now => Time.realtimeSinceStartup;

    public IInput Input { get; }

    public IGameEvents Game { get; }

    public string LastBlankColor => _screenshots.LastBlankColor;

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
        return TestPlugins().Prepend(typeof(GameHost).Assembly).Distinct().SelectMany(StepDescriptorBuilder.FindSteps);
    }

    /// <summary>
    /// The <see cref="InGameTestAttribute"/> methods of each loaded plugin in a <see cref="TestPluginAttribute"/> assembly.
    /// </summary>
    public static IEnumerable<MethodInfo> FindTests()
    {
        return TestPlugins().Distinct().SelectMany(StepDescriptorBuilder.FindTests);
    }

    private static IEnumerable<Assembly> TestPlugins()
    {
        return Chainloader.PluginInfos.Values
            .Where(plugin => plugin.Instance != null)
            .Select(plugin => plugin.Instance.GetType().Assembly)
            .Where(CompiledAgainstTheSdk)
            .Where(StepDescriptorBuilder.IsTestPlugin);
    }

    public IEnumerator EnterStart(ScenarioStart start, string levelFolder)
    {
        return _walk.Enter(start, levelFolder);
    }

    public IEnumerator Screenshot(string name, bool covered = false)
    {
        return _screenshots.Take(name, covered);
    }

    /// <summary>
    /// Only those can be test plugins, and listing an assembly's types throws when another installed mod is missing a dependency.
    /// </summary>
    private static bool CompiledAgainstTheSdk(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies().Any(name => name.Name == typeof(Step).Assembly.GetName().Name);
    }
}
