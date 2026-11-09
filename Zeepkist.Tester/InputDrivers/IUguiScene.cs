using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// The uGUI objects under the pointer, and their handlers, as <see cref="UguiPointer{TNode}"/> reaches them.
/// </summary>
/// <typeparam name="TNode">An object of the scene.</typeparam>
internal interface IUguiScene<TNode>
    where TNode : class
{
    /// <summary>
    /// Whether there's an event system to find what's under a point, which there isn't while the game loads a scene.
    /// </summary>
    public bool Ready { get; }

    /// <summary>
    /// Whether the event system's input module reads the mouse from Unity's <c>Input</c>, which the legacy <c>Input</c>
    /// driver hands the same mouse to.
    /// </summary>
    public bool ModuleReadsUnityInput { get; }

    /// <summary>
    /// The topmost object at the point, or null over none.
    /// </summary>
    /// <param name="screenPoint">In Unity's screen pixels, counted from the bottom left.</param>
    public TNode HitAt(Vector2 screenPoint);

    /// <summary>
    /// Null for an object at the top.
    /// </summary>
    public TNode Parent(TNode node);

    /// <summary>
    /// Whether the object has an active handler for the event.
    /// </summary>
    public bool Handles(TNode node, PointerEvent pointerEvent);

    /// <summary>
    /// Hands the event to the object's handlers for it.
    /// </summary>
    public void Send(TNode node, PointerEvent pointerEvent, PointerState<TNode> state);
}
