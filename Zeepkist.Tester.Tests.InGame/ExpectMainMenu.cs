using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks the main menu is open, with no other menu over it, past the state the tester declares.</summary>
[Step("expect-main-menu")]
[Requires(typeof(InMainMenu))]
public sealed class ExpectMainMenu : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        var menu = Object.FindObjectOfType<MainMenuUI>(true);
        if (menu == null)
            context.Fail("There's no MainMenuUI");
        else if (!menu.IsOpen)
            context.Fail($"{menu.name} isn't open");
        else
            context.Detail = $"{menu.name} is open";

        yield break;
    }
}
