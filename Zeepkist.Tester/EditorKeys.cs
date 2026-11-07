namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Presses the level editor's undo and redo keys for one frame, through the input actions <see cref="LEV_UndoRedo"/>
/// reads them from, and lets the press go again once the editor's update has read it.
/// </summary>
internal static class EditorKeys
{
    private static EditorKey _pressed;

    /// <summary>
    /// Whether the editor still has to read the key pressed last.
    /// </summary>
    public static bool IsPressed => _pressed != EditorKey.None;

    public static void Press(EditorKey key)
    {
        _pressed = key;
    }

    public static void Release()
    {
        _pressed = EditorKey.None;
    }

    public static void BeforeUndoRedoUpdate(LEV_UndoRedo undoRedo)
    {
        var action = GetAction(undoRedo);
        if (action != null)
            action.buttonDown = true;
    }

    public static void AfterUndoRedoUpdate(LEV_UndoRedo undoRedo)
    {
        var action = GetAction(undoRedo);
        if (action == null)
            return;

        action.buttonDown = false;
        Release();
    }

    private static InputActionScriptableObject GetAction(LEV_UndoRedo undoRedo)
    {
        return _pressed switch
        {
            EditorKey.Undo => undoRedo.central.input.Undo,
            EditorKey.Redo => undoRedo.central.input.Redo,
            _ => null,
        };
    }
}
