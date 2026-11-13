using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Changes a block's settings through its editors, as its settings panel does, so the change is one undo step and the level
/// keeps it.
/// </summary>
internal static class BlockEditing
{
    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="setting"/>, and returns it as <see cref="BlockSettings.Read"/>
    /// will once the editors have taken it.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="name">What a failure calls the block.</param>
    /// <param name="setting">The setting's key, as a level file stores it.</param>
    /// <param name="value">The value, as text.</param>
    /// <exception cref="StepFailedException">The block has no settings panel or no such setting, or the value doesn't fit it.</exception>
    public static string ChangeSetting(BlockProperties block, string name, string setting, string value)
    {
        var blockEdits = block.GetAllBlockEditV18sFromThisBlock();
        if (blockEdits.Count == 0)
            throw new StepFailedException($"{name}, block type {block.blockID}, has no settings panel to change a setting in.");

        // The first one registers the undo step, once all of them hold the new value
        blockEdits[0].RegisterUndoRedo();
        var written = BlockSettings.Of(block).Write(setting, value, out var problem)
            ?? throw new StepFailedException(problem);

        foreach (var blockEdit in blockEdits.Skip(1))
            blockEdit.GetUpdateFromSlot_NoUndoRedo(0);

        blockEdits[0].GetUpdateFromSlot(0, updateSlots: true);
        return written;
    }
}
