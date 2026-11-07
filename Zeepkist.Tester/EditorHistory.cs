using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Presses the level editor's undo or redo key once, and fails when the editor's history didn't move, such as with
/// nothing left to undo.
/// </summary>
internal static class EditorHistory
{
    private const int PressFrames = 300;

    public static IEnumerator Press(IStepContext context, EditorKey key)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var before = central.undoRedo.currentHistoryPosition;
        EditorKeys.Press(key);

        // Not WaitUntil: a wait that gives up stops the step, and the key has to be let go first
        for (var frame = 0; EditorKeys.IsPressed && frame < PressFrames; frame++)
            yield return null;

        if (EditorKeys.IsPressed)
        {
            EditorKeys.Release();
            context.Fail($"The level editor didn't read the key within {PressFrames} frames.");
            yield break;
        }

        var after = central.undoRedo.currentHistoryPosition;
        if (after == before)
            context.Fail($"Nothing to {key.ToString().ToLowerInvariant()}, or the editor was busy.");
        else
            context.Detail = $"History position {before} → {after}";
    }
}
