using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Presses the level editor's undo or redo action once, and fails when the editor's history didn't move, such as with
/// nothing left to undo.
/// </summary>
internal static class EditorHistory
{
    private const int ReadySeconds = 5;

    /// <param name="context">The undo or redo step's context.</param>
    /// <param name="action">The game's input action, <c>EditorUndo</c> or <c>EditorRedo</c>.</param>
    /// <param name="what">What it does, for a failure.</param>
    public static IEnumerator Press(IStepContext context, string action, string what)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        // The editor leaves its history alone while a gizmo drags, a level loads or its input is locked
        yield return context.WaitUntil(
            () => !central.gizmos.isDragging && !central.saveload.isLoading && !central.input.inputLocked,
            ReadySeconds,
            "the level editor to take its keys");

        var before = central.undoRedo.currentHistoryPosition;
        yield return context.Input.PressAction(action);
        var after = central.undoRedo.currentHistoryPosition;
        if (after == before)
            context.Fail($"Nothing to {what}.");
        else
            context.Detail = $"History position {before} → {after}";
    }
}
