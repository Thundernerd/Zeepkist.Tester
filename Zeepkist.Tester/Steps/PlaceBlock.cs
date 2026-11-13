using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>
/// Places a block on the ground below the camera, a cell to the side of where place-start puts the start block, and selects
/// it. It's one undo step, like placing it by hand.
/// </summary>
[Step("place-block")]
[Example("- place-block: 2448")]
[Example("- place-block: { type: 2448, as: lamp }")]
[LineSyntax("<type> [as <as>]")]
[Requires(typeof(InEditor))]
public sealed class PlaceBlock : Step
{
    /// <summary>The block's type: its ID in the game's block list, the number a level file stores for it.</summary>
    [Arg(Primary = true)]
    public required int Type { get; set; }

    /// <summary>A name for the block, so later steps can use it, and write <c>${name}</c> for its UID.</summary>
    [Arg]
    public Name<BlockValue>? As { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        context.Blocks.Place(Type, As);
        yield return null;
    }
}
