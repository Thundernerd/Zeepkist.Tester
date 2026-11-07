using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks the level editor is there for a step that needs it, past the state the tester declares.</summary>
[Step("expect-editor")]
[Requires(typeof(InEditor))]
public sealed class ExpectEditor : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        if (context.RequireEditor() is { } editor)
            context.Detail = $"Found {editor.name}";

        yield break;
    }
}
