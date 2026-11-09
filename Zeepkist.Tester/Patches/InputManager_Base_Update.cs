using HarmonyLib;
using Rewired;
using TNRD.Zeepkist.Tester.InputDrivers;

namespace TNRD.Zeepkist.Tester.Patches;

/// <summary>
/// Rewired's update runs first in each frame, and fills the game's input actions from its events. Right after it,
/// <see cref="RewiredDriver"/> writes what the tester holds them at, before the game reads them.
/// </summary>
[HarmonyPatch(typeof(InputManager_Base), "Update")]
internal static class InputManager_Base_Update
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RewiredDriver.AfterRewiredUpdate();
    }
}
