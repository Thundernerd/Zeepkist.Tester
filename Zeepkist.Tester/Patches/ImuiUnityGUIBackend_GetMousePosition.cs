using HarmonyLib;
using Imui.IO.UGUI;
using TNRD.Zeepkist.Tester.InputDrivers;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Patches;

/// <summary>
/// Imui finds the mouse where <see cref="ImuiDriver"/> put it, while it holds one, in its own pixels as it does the player's.
/// </summary>
[HarmonyPatch(typeof(ImuiUnityGUIBackend), nameof(ImuiUnityGUIBackend.GetMousePosition))]
internal static class ImuiUnityGUIBackend_GetMousePosition
{
    [HarmonyPrefix]
    private static bool Prefix(ImuiUnityGUIBackend __instance, ref Vector2 __result)
    {
        if (ImuiDriver.Position is not { } position)
            return true;

        __result = (position - __instance.GetWorldRect().position) / __instance.GetScale();
        return false;
    }
}
