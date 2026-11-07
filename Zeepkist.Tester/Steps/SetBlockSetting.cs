using System.Collections;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>
/// Changes a setting of a named block, as changing it in the block's settings panel does: through the block's editor, so it's
/// one undo step and the level keeps it.
/// </summary>
[Step("set-block-setting")]
[Example("- set-block-setting: { block: telemetry, setting: xbs, value: 2 }")]
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
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var uid = Block.Get(context).Uid;
        if (!central.undoRedo.allBlocksDictionary.TryGetValue(uid, out var block) || block == null)
        {
            context.Fail($"The level has no block with {Block}'s UID \"{uid}\" any more.");
            yield break;
        }

        var blockEdits = block.GetAllBlockEditV18sFromThisBlock();
        if (blockEdits.Count == 0)
        {
            context.Fail($"{Block}, block type {block.blockID}, has no settings panel to change a setting in.");
            yield break;
        }

        // The first one registers the undo step, once all of them hold the new value
        blockEdits[0].RegisterUndoRedo();
        var written = BlockSettings.Of(block).Write(Setting, Value, out var problem);
        if (written == null)
        {
            context.Fail(problem);
            yield break;
        }

        foreach (var blockEdit in blockEdits.Skip(1))
            blockEdit.GetUpdateFromSlot_NoUndoRedo(0);

        var historyBefore = central.undoRedo.currentHistoryPosition;
        blockEdits[0].GetUpdateFromSlot(0, updateSlots: true);
        yield return null;

        var kept = BlockSettings.Of(block).Read(Setting);
        if (kept != written)
            context.Fail($"The block didn't keep {Setting} {Value}.", written, kept);
        else
            context.Detail = $"{Block}: {Setting} = {kept}, history position {historyBefore} → {central.undoRedo.currentHistoryPosition}";
    }
}
