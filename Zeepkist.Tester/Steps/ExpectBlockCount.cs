using System.Collections;
using System.Globalization;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Counts the level's blocks, or only those of one type.</summary>
[Step("expect-block-count")]
[Example("- expect-block-count: 3")]
[Example("- expect-block-count: { count: 1, type: 2448 }")]
[Requires(typeof(InEditor))]
public sealed class ExpectBlockCount : Step
{
    /// <summary>How many there should be.</summary>
    [Arg(Primary = true)]
    public required int Count { get; set; }

    /// <summary>Only counts blocks of this type: its ID in the game's block list, the number a level file stores for it.</summary>
    [Arg]
    public int? Type { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var count = central.undoRedo.allBlocksDictionary.Values.Count(block => block != null && (Type == null || block.blockID == Type));
        var what = (count == 1 ? "block" : "blocks") + (Type == null ? string.Empty : $" of type {Type}");
        var expected = Count.ToString(CultureInfo.InvariantCulture);
        var actual = count.ToString(CultureInfo.InvariantCulture);
        if (count == Count)
            context.Pass($"{count} {what}", expected, actual);
        else
            context.Fail($"{count} {what}", expected, actual);
    }
}
