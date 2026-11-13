using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Opens the main menu's Level Editor page, then clicks the page's Level Editor button at its position, which should
/// enter the level editor. It takes uGUI's hover and Unity's Input press together.
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
        var page = new LevelEditorButton();
        yield return page.Open(context);
        if (page.Button == null)
            yield break;

        var entriesBefore = context.Game.EditorEntries;
        yield return context.Input.Click(page.Center);
        yield return context.WaitUntil(() => context.Game.EditorEntries > entriesBefore, new Seconds(LoadSeconds), "the level editor");
    }
}
