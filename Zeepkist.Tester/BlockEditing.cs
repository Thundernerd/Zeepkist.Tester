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
    /// <param name="central">The level editor.</param>
    /// <param name="block">The block whose setting changes.</param>
    /// <param name="name">What a failure calls the block: the scenario's name for it.</param>
    /// <param name="setting">The setting's key, as a level file stores it.</param>
    /// <param name="value">The value as a scenario writes it, which <see cref="BlockSettings.Write"/> reads.</param>
    /// <param name="historyBefore">The editor's history position just before the change registers its undo step.</param>
    /// <exception cref="StepFailedException">The block has no settings panel or no such setting, or the value doesn't fit it.</exception>
    public static string ChangeSetting(LEV_LevelEditorCentral central, BlockProperties block, string name, string setting, string value, out int historyBefore)
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

        historyBefore = central.undoRedo.currentHistoryPosition;
        blockEdits[0].GetUpdateFromSlot(0, updateSlots: true);
        return written;
    }
}
