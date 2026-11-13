using System;
using System.Collections;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The level editor's blocks for one step, with <see cref="BlockPlacing"/> and <see cref="BlockSettings"/> doing the game's part.
/// </summary>
internal sealed class EditorBlocks : IEditorBlocks
{
    private readonly IStepContext _context;
    private readonly Func<LEV_LevelEditorCentral> _editor;

    /// <param name="context">The step it changes blocks for, whose named values and detail it uses.</param>
    /// <param name="editor">The level editor, or null outside it.</param>
    public EditorBlocks(IStepContext context, Func<LEV_LevelEditorCentral> editor)
    {
        _context = context;
        _editor = editor;
    }

    public BlockValue Place(int type, Name<BlockValue>? name = null)
    {
        var central = RequireEditor();
        var types = central.manager.loader.globalBlockList.blocks;
        if (type < 0 || type >= types.Count || types[type] == null)
            throw new StepFailedException($"The game has no block type {type}.");

        // Before placing, so a name that's already taken places nothing
        var placed = new BlockValue(BlockPlacing.NewUid(type));
        name?.Set(_context, placed);

        var block = BlockPlacing.Place(central, type, placed.Uid, yaw: 0, cellsToTheSide: 1, "Test: place block");
        BlockPlacing.Select(central, block);
        _context.Detail = $"Block type {type}, UID \"{placed.Uid}\" at {block.transform.position}";
        return placed;
    }

    public int TypeOf(Ref<BlockValue> block)
    {
        return Find(RequireEditor(), block).blockID;
    }

    public IEnumerator SetSetting(Ref<BlockValue> block, string key, string value)
    {
        var central = RequireEditor();
        var properties = Find(central, block);

        var blockEdits = properties.GetAllBlockEditV18sFromThisBlock();
        if (blockEdits.Count == 0)
            throw new StepFailedException($"{block}, block type {properties.blockID}, has no settings panel to change a setting in.");

        // The first one registers the undo step, once all of them hold the new value
        blockEdits[0].RegisterUndoRedo();
        var written = BlockSettings.Of(properties).Write(key, value, out var problem);
        if (written == null)
            throw new StepFailedException(problem);

        foreach (var blockEdit in blockEdits.Skip(1))
            blockEdit.GetUpdateFromSlot_NoUndoRedo(0);

        var historyBefore = central.undoRedo.currentHistoryPosition;
        blockEdits[0].GetUpdateFromSlot(0, updateSlots: true);
        yield return null;

        var kept = BlockSettings.Of(properties).Read(key);
        if (kept != written)
            _context.Fail($"The block didn't keep {key} {value}.", written, kept);
        else
            _context.Detail = $"{block}: {key} = {kept}, history position {historyBefore} → {central.undoRedo.currentHistoryPosition}";
    }

    private BlockProperties Find(LEV_LevelEditorCentral central, Ref<BlockValue> block)
    {
        var uid = block.Get(_context).Uid;
        if (!central.undoRedo.allBlocksDictionary.TryGetValue(uid, out var properties) || properties == null)
            throw new StepFailedException($"The level has no block with {block}'s UID \"{uid}\" any more.");

        return properties;
    }

    private LEV_LevelEditorCentral RequireEditor()
    {
        return _editor() ?? throw new StepFailedException(StepContext.NotInEditor);
    }
}
