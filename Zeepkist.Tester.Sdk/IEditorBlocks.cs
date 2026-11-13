using System.Collections;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The level editor's blocks, changed as the core steps <c>place-block</c> and <c>set-block-setting</c> change them, which are
/// thin wrappers over this. A mod's step uses it the same way, after finding a block's type or a setting's key by names of its own.
/// Each stops the step and fails it, with a <see cref="StepFailedException"/>, outside the level editor and when the game won't
/// take the change.
/// </summary>
public interface IEditorBlocks
{
    /// <summary>
    /// Places a block on the ground below the camera, a cell to the side of where <c>place-start</c> puts the start block, and
    /// selects it. It's one undo step, like placing it by hand. Fails the step for a type the game doesn't have, for a
    /// <paramref name="name"/> the scenario already defined, which places nothing, and while blocks are grabbed.
    /// </summary>
    /// <param name="type">The block's ID in the game's block list, the number a level file stores for it.</param>
    /// <param name="name">A name for the block, so later steps can use it and write <c>${name}</c> for its UID.</param>
    /// <returns>The block, whose UID <see cref="IStepContext.Detail"/> also gives.</returns>
    public BlockValue Place(int type, Name<BlockValue>? name = null);

    /// <summary>
    /// The block's type: its ID in the game's block list, such as to look up its settings by names of a mod's own. Fails the step
    /// when the level no longer has the block.
    /// </summary>
    public int TypeOf(Ref<BlockValue> block);

    /// <summary>
    /// Changes a setting of a block, as changing it in the block's settings panel does: through the block's editor, so it's one
    /// undo step and the level keeps it. Fails the step when the level no longer has the block, when it has no settings panel or
    /// no setting under <paramref name="key"/>, or when <paramref name="value"/> doesn't fit the setting. A block that didn't keep
    /// the value fails the step with <see cref="IStepContext.Fail"/> after the change, so a caller that goes on checks
    /// <see cref="IStepContext.Failed"/>. Says what it did in <see cref="IStepContext.Detail"/>, as <see cref="Place"/> does.
    /// </summary>
    /// <param name="block">The block, as <see cref="Place"/> or a mod's step named it.</param>
    /// <param name="key">The setting's key, as a level file stores it. A failure lists the keys the block has.</param>
    /// <param name="value">A whole number, <c>true</c> or <c>false</c>, a decimal, or text, as fits the setting.</param>
    public IEnumerator SetSetting(Ref<BlockValue> block, string key, string value);
}
