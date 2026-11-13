using System.Collections;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks the text a named sign's own editor holds, which isn't where a step wrote it: that's the level's copy.</summary>
[Step("expect-sign-text")]
[Requires(typeof(InEditor))]
public sealed class ExpectSignText : Step
{
    /// <summary>The sign.</summary>
    [Arg]
    public required Ref<BlockValue> Block { get; set; }

    /// <summary>The text it should show.</summary>
    [Arg]
    public required string Text { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var block = central.undoRedo.allBlocksDictionary[Block.Get(context).Uid];
        var actual = block.GetAllBlockEditV18sFromThisBlock().OfType<BlockEdit_v18_WriteableSign>().Single().writeableText;
        context.Check(actual == Text, $"{Block} shows {actual}", Text, actual);
    }
}
