using HarmonyLib;
using TNRD.Zeepkist.Tester.InputDrivers;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Patches;

/// <summary>
/// Unity's own <c>Input</c> asks <see cref="Driver"/> first, so a key or button it holds is down on top of the player's,
/// its mouse is where it put it, and its axes add to the player's. The mouse buttons, <c>anyKey</c> and the axes are
/// native methods, which HarmonyX patches too.
/// </summary>
[HarmonyPatch(typeof(Input))]
internal static class Input_Legacy
{
    /// <summary>
    /// Null while there's none, and <c>Input</c> is only the player's.
    /// </summary>
    public static LegacyInputDriver Driver { get; set; }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetKey), typeof(KeyCode))]
    private static bool GetKey(KeyCode key, ref bool __result)
    {
        return Answer(Driver?.GetKey(key), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetKeyDown), typeof(KeyCode))]
    private static bool GetKeyDown(KeyCode key, ref bool __result)
    {
        return Answer(Driver?.GetKeyDown(key), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetKeyUp), typeof(KeyCode))]
    private static bool GetKeyUp(KeyCode key, ref bool __result)
    {
        return Answer(Driver?.GetKeyUp(key), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetKey), typeof(string))]
    private static bool GetKeyByName(string name, ref bool __result)
    {
        return Answer(Driver?.GetKey(name), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetKeyDown), typeof(string))]
    private static bool GetKeyDownByName(string name, ref bool __result)
    {
        return Answer(Driver?.GetKeyDown(name), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetKeyUp), typeof(string))]
    private static bool GetKeyUpByName(string name, ref bool __result)
    {
        return Answer(Driver?.GetKeyUp(name), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetMouseButton))]
    private static bool GetMouseButton(int button, ref bool __result)
    {
        return Answer(Driver?.GetMouseButton(button), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetMouseButtonDown))]
    private static bool GetMouseButtonDown(int button, ref bool __result)
    {
        return Answer(Driver?.GetMouseButtonDown(button), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.GetMouseButtonUp))]
    private static bool GetMouseButtonUp(int button, ref bool __result)
    {
        return Answer(Driver?.GetMouseButtonUp(button), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.anyKey), MethodType.Getter)]
    private static bool AnyKey(ref bool __result)
    {
        return Answer(Driver?.AnyKey, ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.anyKeyDown), MethodType.Getter)]
    private static bool AnyKeyDown(ref bool __result)
    {
        return Answer(Driver?.AnyKeyDown, ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Input.mousePosition), MethodType.Getter)]
    private static bool MousePosition(ref Vector3 __result)
    {
        if (Driver?.MousePosition is not { } position)
            return true;

        __result = position;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Input.GetAxis))]
    private static void GetAxis(string axisName, ref float __result)
    {
        if (Driver != null)
            __result = Driver.Axis(axisName, __result);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Input.GetAxisRaw))]
    private static void GetAxisRaw(string axisName, ref float __result)
    {
        if (Driver != null)
            __result = Driver.AxisRaw(axisName, __result);
    }

    /// <summary>
    /// True from the driver is the answer; otherwise the player's input answers.
    /// </summary>
    private static bool Answer(bool? driver, ref bool result)
    {
        if (driver != true)
            return true;

        result = true;
        return false;
    }
}
