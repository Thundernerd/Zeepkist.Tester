using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Opens the main menu's Level Editor page as its button does, then clicks the page's Level Editor button at its
/// position, which should enter the level editor. It takes uGUI's hover and Unity's Input press together.
/// </summary>
[Step("click-level-editor-button")]
[Requires(typeof(InMainMenu))]
[Leaves(typeof(InMainMenu))]
[Enters(typeof(InEditor))]
public sealed class ClickLevelEditorButton : Step
{
    private const float LoadSeconds = 60;

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

        var entriesBefore = context.Game.EditorEntries;
        yield return context.Input.Click(ScreenPoints.Center((RectTransform)button.transform));
        yield return context.WaitUntil(() => context.Game.EditorEntries > entriesBefore, new Seconds(LoadSeconds), "the level editor");
    }
}
