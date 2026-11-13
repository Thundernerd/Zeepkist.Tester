using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Where a block is, for pointing the editor's camera at it.
/// </summary>
internal static class BlockCenter
{
    /// <summary>
    /// The middle of all of the block's colliders, or where it stands if it has none.
    /// </summary>
    public static Vector3 Of(BlockProperties block)
    {
        var colliders = block.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return block.transform.position;

        var bounds = colliders[0].bounds;
        foreach (var collider in colliders)
            bounds.Encapsulate(collider.bounds);

        return bounds.center;
    }
}
