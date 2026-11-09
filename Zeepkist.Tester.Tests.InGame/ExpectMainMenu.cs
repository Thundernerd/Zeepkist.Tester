using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks the main menu's Level Editor panel is there, past the state the tester declares.</summary>
[Step("expect-main-menu")]
[Requires(typeof(InMainMenu))]
public sealed class ExpectMainMenu : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        if (Object.FindObjectOfType<LevelEditorUI>(true) is { } menu)
            context.Detail = $"Found {menu.name}";
        else
            context.Fail("The main menu has no LevelEditorUI.");

        yield break;
    }
}
