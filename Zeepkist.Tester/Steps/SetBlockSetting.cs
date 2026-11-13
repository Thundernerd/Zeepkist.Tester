using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>
/// Changes a setting of a named block, as changing it in the block's settings panel does: through the block's editor, so it's
/// one undo step and the level keeps it.
/// </summary>
[Step("set-block-setting")]
[Example("- set-block-setting: { block: telemetry, setting: xbs, value: 2 }")]
[LineSyntax("<block> <setting> <value>")]
[Requires(typeof(InEditor))]
public sealed class SetBlockSetting : Step
{
    /// <summary>The block, as <c>place-block</c> named it.</summary>
    [Arg]
    public required Ref<BlockValue> Block { get; set; }

    /// <summary>The setting's key, as a level file stores it. A failure lists the keys the block has.</summary>
    [Arg]
    public required string Setting { get; set; }

    /// <summary>The value, which has to fit the setting: a whole number, <c>true</c> or <c>false</c>, a decimal, or text.</summary>
    [Arg]
    public required string Value { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        return context.Blocks.SetSetting(Block, Setting, Value);
    }
}
