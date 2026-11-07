using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Presses the level editor's undo key once, and fails when that undid nothing.</summary>
[Step("undo")]
[Example("- undo")]
[Requires(typeof(InEditor))]
public sealed class Undo : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        return EditorHistory.Press(context, EditorKey.Undo);
    }
}
