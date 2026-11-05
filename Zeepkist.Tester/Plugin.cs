using System;
using System.IO;
using BepInEx;
using HarmonyLib;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Runs the tests inside the game and records what it finds, for the runner. It does nothing in normal play: a run needs
/// both the runner's command-line flag and a run.json next to this DLL.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(ZeepSdkInfo.PluginGuid, ZeepSdkInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    private Harmony _harmony;
    private TestRun _run;

    private void Awake()
    {
        var folder = Path.GetDirectoryName(Info.Location);
        if (!RunRequest.TryRead(folder, Environment.GetCommandLineArgs(), out var request))
            return;

        Logger.LogInfo($"{TestRun.Marker} run {request.RunId} starting");
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
        var exceptions = new ExceptionLog();
        var results = new RunResults(request.RunId, Path.Combine(folder, "out"), exceptions.Logged);
        _run = new TestRun(Logger, results, exceptions, request, folder);
        StartCoroutine(_run.Run());
    }

    private void OnDestroy()
    {
        _run?.Dispose();
        _harmony?.UnpatchSelf();
    }
}
