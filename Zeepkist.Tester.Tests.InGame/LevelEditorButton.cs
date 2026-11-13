using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// The main menu's Level Editor page's button that starts the editor, a <see cref="GenericButton"/> with no uGUI click
/// handler: uGUI hovers it, and its own Update reads the press from Unity's Input.
/// </summary>
public static class LevelEditorButton
{
    /// <summary>
    /// The button wired to the page's <see cref="LevelEditorUI.StartLevelEditor"/>, or null when there's none.
    /// </summary>
    public static GenericButton Find(LevelEditorUI menu)
    {
        foreach (var button in Object.FindObjectsOfType<GenericButton>())
        {
            for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == menu
                    && button.onClick.GetPersistentMethodName(i) == nameof(LevelEditorUI.StartLevelEditor))
                    return button;
            }
        }

        return null;
    }
}
