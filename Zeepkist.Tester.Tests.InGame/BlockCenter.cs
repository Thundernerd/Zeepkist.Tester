using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Where a block is, for pointing the editor's camera and mouse at it.
/// </summary>
internal static class BlockCenter
{
    /// <summary>
    /// The middle of what a click on the block hits, or where it stands if it has nothing to hit.
    /// </summary>
    public static Vector3 Of(LEV_LevelEditorCentral central, string uid)
    {
        var block = central.undoRedo.allBlocksDictionary[uid];
        var colliders = block.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return block.transform.position;

        var bounds = colliders[0].bounds;
        foreach (var collider in colliders)
            bounds.Encapsulate(collider.bounds);

        return bounds.center;
    }
}
