using HarmonyLib;

namespace TNRD.Zeepkist.Tester.Patches;

/// <summary>
/// Holds the key <see cref="EditorKeys"/> pressed down for the one update that reads it.
/// </summary>
[HarmonyPatch(typeof(LEV_UndoRedo), nameof(LEV_UndoRedo.Update))]
internal static class LEV_UndoRedo_Update
{
    [HarmonyPrefix]
    private static void Prefix(LEV_UndoRedo __instance)
    {
        EditorKeys.BeforeUndoRedoUpdate(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(LEV_UndoRedo __instance)
    {
        EditorKeys.AfterUndoRedoUpdate(__instance);
    }
}
