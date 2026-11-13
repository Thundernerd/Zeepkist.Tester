using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Opens the main menu's Level Editor page as its button does, moves the mouse onto the page's Level Editor button, which
/// should take it as hovered, then gives the mouse back, which should leave it. Clicking it is <see cref="ClickLevelEditorButton"/>'s.
/// </summary>
[Step("hover-level-editor-button")]
public sealed class HoverLevelEditorButton : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var menu = Object.FindObjectOfType<LevelEditorUI>(true);
        if (menu == null)
        {
            context.Fail("The main menu has no Level Editor page");
            yield break;
        }

        menu.Open(true);

        // The page lays out as it draws
        yield return null;
        yield return null;
        var button = LevelEditorButton.Find(menu);
        if (button == null)
        {
            context.Fail("The main menu has no Level Editor button");
            yield break;
        }

        yield return context.Input.MoveMouse(ScreenPoints.Center((RectTransform)button.transform));
        if (!button.isHover)
        {
            context.Fail("Moving the mouse onto the Level Editor button didn't hover it", "hovered", "not hovered");
            yield break;
        }

        context.Input.GiveBack();
        yield return null;
        if (button.isHover)
            context.Fail("Giving the mouse back didn't leave the Level Editor button", "not hovered", "hovered");

        menu.Close(true);
    }
}
