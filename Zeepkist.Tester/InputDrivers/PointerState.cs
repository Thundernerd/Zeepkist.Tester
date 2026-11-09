using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// The pointer as an event finds it, which the scene hands its handlers as Unity's <c>PointerEventData</c>.
/// </summary>
/// <typeparam name="TNode">An object of the scene.</typeparam>
internal sealed class PointerState<TNode>
    where TNode : class
{
    /// <summary>
    /// In Unity's screen pixels, counted from the bottom left.
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// The topmost object under the pointer, or null over none.
    /// </summary>
    public TNode Hit { get; set; }

    public MouseButton Button { get; set; }

    /// <summary>
    /// What took the press of <see cref="Button"/>, or null.
    /// </summary>
    public TNode Pressed { get; set; }

    /// <summary>
    /// Where the pointer was as <see cref="Button"/> went down.
    /// </summary>
    public Vector2 PressPosition { get; set; }
}
