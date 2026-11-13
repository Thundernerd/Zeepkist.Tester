using System;
using System.Collections.Generic;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Places a block on the ground below the editor's camera as one undo step, as placing it by hand is.
/// </summary>
internal static class BlockPlacing
{
    private const float GridSize = 16;
    private const string PaintKeyStart = "p";

    /// <summary>
    /// A UID no block in the level has, for <see cref="Place"/>.
    /// </summary>
    public static string NewUid(int blockId)
    {
        return PlayerManager.Instance.GenerateUniqueIDforBlocks(blockId.ToString(CultureInfo.InvariantCulture));
    }

    /// <param name="central">The level editor.</param>
    /// <param name="blockId">The type of the block.</param>
    /// <param name="uid">The block's UID, from <see cref="NewUid"/>.</param>
    /// <param name="yaw">Which way the block faces.</param>
    /// <param name="cellsToTheSide">How many cells along x from below the camera, so one block doesn't go where another does.</param>
    /// <param name="label">What the game's logs call this.</param>
    /// <exception cref="StepFailedException">A grabbed block keeps the editor from registering the undo step.</exception>
    public static BlockProperties Place(LEV_LevelEditorCentral central, int blockId, string uid, float yaw, int cellsToTheSide, string label)
    {
        // BreakLock silently skips the undo step while grabbing
        if (central.gizmos.isGrabbing)
            throw new StepFailedException("Can't place a block while blocks are grabbed.");

        var camera = central.cam.cameraTransform;
        var position = new Vector3((Mathf.Round(camera.position.x / GridSize) + cellsToTheSide) * GridSize, 0, Mathf.Round(camera.position.z / GridSize) * GridSize);
        var json = new BlockPropertyJSON
        {
            i = blockId,
            u = uid,
            p = new CV3(position),
            r = new CV3(new Vector3(0, yaw, 0)),
            s = new CV3(Vector3.one),
            d = new PropertyDictionariesJSON(),
        };

        BlockProperties block = null;
        try
        {
            AddDefaultPaint(central.BlockTypes()[blockId], json);

            // Private: callable because AllowUnsafeBlocks makes Mono skip its access checks for this assembly
            block = central.undoRedo.GenerateNewBlock(json, json.u);
            block.NewBlockCreatedFromEditorForV15JSON();
            StaticConnectorTracker.AddBlockToTracker(block, label);
            LoadConnectors(block, label);
            RegisterUndo(central, block, label);
            return block;
        }
        catch (Exception)
        {
            if (block != null)
                Remove(central, block);

            throw;
        }
    }

    /// <summary>
    /// Selects the block alone, as clicking it does.
    /// </summary>
    public static void Select(LEV_LevelEditorCentral central, BlockProperties block)
    {
        var selection = central.selection;
        var before = central.undoRedo.ConvertSelectionToStringList(selection.list);
        selection.DeselectAllBlocks(false, nameof(Select));
        selection.AddThisBlock(block);
        selection.RegisterManualSelectionBreakLock(before, central.undoRedo.ConvertSelectionToStringList(selection.list));
    }

    /// <summary>
    /// Paint is stored per renderer material slot as n["p{index}"], and a missing key loads as material 0. Writes the default
    /// paint that placing the block by hand would, which GenerateNewBlock leaves out.
    /// </summary>
    private static void AddDefaultPaint(BlockProperties prefab, BlockPropertyJSON json)
    {
        var painter = prefab.GetComponent<Properties_RoadPainter>();
        if (painter == null || !MaterialManager.DoesExist())
            return;

        var slot = 0;
        foreach (var renderer in painter.renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                json.d.SetIntDictionaryValue(PaintKeyStart + slot, MaterialManager.GetMaterialHolderFromMaterial(material).materialID);
                slot++;
            }
        }
    }

    /// <summary>
    /// LoadProperties_v15 leaves the Connector scripts out.
    /// </summary>
    private static void LoadConnectors(BlockProperties block, string label)
    {
        foreach (var blockEdit in block.GetAllBlockEditV18sFromThisBlock())
        {
            if (!blockEdit.HasConnectors())
                continue;

            var connections = blockEdit.GetBlockEditV18ConnectionBase();
            connections.LoadProperties();
            connections.ForceRedrawConnectionVisualizers(label);
        }
    }

    /// <summary>
    /// An added block is recorded as null / JSON, the shape the editor's own undo steps use.
    /// </summary>
    private static void RegisterUndo(LEV_LevelEditorCentral central, BlockProperties block, string label)
    {
        List<string> before = [null];
        List<string> after = [block.ConvertBlockToJSON_v15_string(true)];
        List<BlockProperties> blocks = [block];
        var collection = central.undoRedo.ConvertBeforeAndAfterListToCollection(before, after, blocks, [], []);
        central.validation.BreakLock(collection, label);
    }

    private static void Remove(LEV_LevelEditorCentral central, BlockProperties block)
    {
        central.undoRedo.allBlocksDictionary.Remove(block.UID);
        central.selection.list.Remove(block);
        StaticConnectorTracker.RemoveBlockFromTracker(block.UID);
        UnityEngine.Object.Destroy(block.gameObject);
    }
}
