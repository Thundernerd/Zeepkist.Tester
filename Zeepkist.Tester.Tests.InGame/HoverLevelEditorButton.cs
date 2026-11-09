using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Moves the mouse onto the main menu's Level Editor button, which should take it as hovered, then gives the mouse back,
/// which should leave it. The button reads its press from Unity's <c>Input</c>, which the uGUI driver doesn't reach.
/// </summary>
[Step("hover-level-editor-button")]
public sealed class HoverLevelEditorButton : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var button = FindLevelEditorButton();
        if (button == null)
        {
            context.Fail("The main menu has no Level Editor button");
            yield break;
        }

        yield return context.Input.MoveMouse(ScreenCenter(button));
        if (!button.isHover)
        {
            context.Fail("Moving the mouse onto the Level Editor button didn't hover it", "hovered", "not hovered");
            yield break;
        }

        context.Input.GiveBack();
        yield return null;
        if (button.isHover)
            context.Fail("Giving the mouse back didn't leave the Level Editor button", "not hovered", "hovered");
    }

    private static GenericButton FindLevelEditorButton()
    {
        var menu = Object.FindObjectOfType<LevelEditorUI>();
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

    private static Vector2 ScreenCenter(Component button)
    {
        var rect = (RectTransform)button.transform;
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }
}
