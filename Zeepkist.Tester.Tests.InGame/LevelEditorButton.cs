using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// The main menu's Level Editor page's button that starts the editor, a <see cref="GenericButton"/> with no uGUI click
/// handler: uGUI hovers it, and its own Update reads the press from Unity's Input.
/// </summary>
public sealed class LevelEditorButton
{
    private LevelEditorUI _menu;

    /// <summary>
    /// The button, once <see cref="Open"/> has found it, or null.
    /// </summary>
    public GenericButton Button { get; private set; }

    /// <summary>
    /// The middle of the button, in screen pixels.
    /// </summary>
    public Vector2 Center => ScreenPoints.Center((RectTransform)Button.transform);

    /// <summary>
    /// Opens the Level Editor page as its button on the main menu does, and finds the button on it, failing the step
    /// and leaving <see cref="Button"/> null when either isn't there.
    /// </summary>
    public IEnumerator Open(IStepContext context)
    {
        _menu = Object.FindObjectOfType<LevelEditorUI>(true);
        if (_menu == null)
        {
            context.Fail("The main menu has no Level Editor page");
            yield break;
        }

        _menu.Open(true);

        // The page lays out as it draws
        yield return null;
        yield return null;
        Button = Find(_menu);
        if (Button == null)
            context.Fail("The main menu has no Level Editor button");
    }

    /// <summary>
    /// Closes the page <see cref="Open"/> opened.
    /// </summary>
    public void Close()
    {
        _menu.Close(true);
    }

    private static GenericButton Find(LevelEditorUI menu)
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
