using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The level editor's blocks, and the types of block the game has, for steps that work with them.
/// </summary>
public static class LevelEditorBlocks
{
    /// <summary>
    /// Every type of block the game has, by its ID: the number a level file stores for it. Some IDs have none, so an item
    /// can be null.
    /// </summary>
    public static List<BlockProperties> BlockTypes(this LEV_LevelEditorCentral central)
    {
        return central.manager.loader.globalBlockList.blocks;
    }

    /// <summary>
    /// The blocks the level has.
    /// </summary>
    public static IEnumerable<BlockProperties> Blocks(this LEV_LevelEditorCentral central)
    {
        return central.undoRedo.allBlocksDictionary.Values.Where(block => block != null);
    }

    /// <summary>
    /// The level's block that <paramref name="block"/> names. Fails the step, with a <see cref="StepFailedException"/>, when
    /// the scenario has no such name or the level no longer has the block.
    /// </summary>
    public static BlockProperties GetBlock(this LEV_LevelEditorCentral central, IStepContext context, Ref<BlockValue> block)
    {
        var uid = block.Get(context).Uid;
        if (central.undoRedo.allBlocksDictionary.TryGetValue(uid, out var found) && found != null)
            return found;

        throw new StepFailedException($"The level has no block with {block}'s UID \"{uid}\" any more.");
    }
}
