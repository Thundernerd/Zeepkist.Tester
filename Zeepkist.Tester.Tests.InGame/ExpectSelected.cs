using System.Collections;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks the named block is the only one selected.</summary>
[Step("expect-selected")]
[Requires(typeof(InEditor))]
public sealed class ExpectSelected : Step
{
    /// <summary>The block.</summary>
    [Arg(Primary = true)]
    public required Ref<BlockValue> Block { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var expected = Block.Get(context).Uid;
        var selected = string.Join(", ", central.selection.list.Select(block => block.UID));
        if (selected == expected)
            context.Pass($"{Block} is selected", expected, selected);
        else
            context.Fail($"{Block} isn't the only one selected", expected, selected);
    }
}
