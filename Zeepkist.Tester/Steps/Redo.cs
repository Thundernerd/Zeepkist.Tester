using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Presses the level editor's redo key once, and fails when that redid nothing.</summary>
[Step("redo")]
[Example("- redo")]
[Requires(typeof(InEditor))]
public sealed class Redo : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        return EditorHistory.Press(context, "EditorRedo", "redo");
    }
}
