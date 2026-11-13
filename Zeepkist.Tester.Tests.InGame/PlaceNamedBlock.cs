using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Places a block by its name in this plugin's list, as a mod's step does with <c>context.Blocks</c>.</summary>
[Step("place-named-block")]
[Example("- sample.place-named-block: { function: lamp, as: lamp }")]
[Requires(typeof(InEditor))]
public sealed class PlaceNamedBlock : Step
{
    /// <summary>The block's name: lamp or telemetry.</summary>
    [Arg(Primary = true)]
    public required string Function { get; set; }

    /// <summary>A name for the block.</summary>
    [Arg]
    public Name<BlockValue>? As { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        if (!NamedBlocks.TryGetType(Function, out var type))
            throw new StepFailedException($"There's no block called {Function}. There are {NamedBlocks.Names}.");

        context.Blocks.Place(type, As);
        context.Detail = $"{Function}: {context.Detail}";
        yield return null;
    }
}
