using System;
using HarmonyLib;

namespace TNRD.Zeepkist.Tester.Patches;

/// <summary>
/// ZeepSDK's LevelLoaded leaves out loads that keep the undo history, such as the one coming back from test mode.
/// </summary>
[HarmonyPatch(typeof(LEV_SaveLoad), nameof(LEV_SaveLoad.ExternalLoad))]
internal static class LEV_SaveLoad_ExternalLoad
{
    public static event Action Loaded;

    [HarmonyPostfix]
    private static void Postfix()
    {
        Loaded?.Invoke();
    }
}
