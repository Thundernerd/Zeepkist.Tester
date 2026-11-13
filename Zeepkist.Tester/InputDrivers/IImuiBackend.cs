using Imui.IO.Events;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Imui's backend, as <see cref="ImuiDriver"/> hands it input, and what uGUI finds above it.
/// </summary>
internal interface IImuiBackend
{
    /// <summary>
    /// Whether there's a backend to hand input to.
    /// </summary>
    public bool Present { get; }

    /// <summary>
    /// Where Imui finds the mouse, in its own pixels.
    /// </summary>
    public Vector2 MousePosition { get; }

    /// <summary>
    /// Screen pixels to one of Imui's.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// What uGUI finds on top at the point.
    /// </summary>
    /// <param name="screenPoint">In Unity's screen pixels, counted from the bottom left.</param>
    public UguiTop TopAt(Vector2 screenPoint);

    /// <summary>
    /// Queues a key's event for Imui's next frame.
    /// </summary>
    public void QueueKey(ImKeyboardEventType type, KeyCode key, EventModifiers modifiers, char character);

    /// <summary>
    /// Queues a mouse button's event, which Imui takes one a frame.
    /// </summary>
    /// <param name="type">What the button does.</param>
    /// <param name="button">The button.</param>
    /// <param name="delta">How far a drag moves, in Imui's pixels.</param>
    /// <param name="count">How many clicks in a row a press is, which the backend gives only its Down events.</param>
    public void QueueMouse(ImMouseEventType type, MouseButton button, Vector2 delta, int count);
}
