using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Opens the main menu's Level Editor page, moves the mouse onto the page's Level Editor button, which should take it as
/// hovered, then gives the mouse back, which should leave it. Clicking it is
/// <see cref="ClickLevelEditorButton"/>'s.
/// </summary>
[Step("hover-level-editor-button")]
public sealed class HoverLevelEditorButton : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var page = new LevelEditorButton();
        yield return page.Open(context);
        if (page.Button == null)
            yield break;

        yield return context.Input.MoveMouse(page.Center);
        if (!page.Button.isHover)
        {
            context.Fail("Moving the mouse onto the Level Editor button didn't hover it", "hovered", "not hovered");
            yield break;
        }

        context.Input.GiveBack();
        yield return null;
        if (page.Button.isHover)
            context.Fail("Giving the mouse back didn't leave the Level Editor button", "not hovered", "hovered");

        page.Close();
    }
}
