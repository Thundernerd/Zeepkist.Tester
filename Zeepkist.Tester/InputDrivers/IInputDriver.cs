using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// One way of handing the game input. <see cref="TesterInput"/> decides what goes in which frame. Each call says whether
/// there was anything to take the input, as there isn't while what the driver hands it to doesn't exist.
/// </summary>
internal interface IInputDriver
{
    /// <summary>
    /// The keys go down, in order, and each stays down until <see cref="ReleaseKeys"/> or <see cref="GiveBack"/>.
    /// </summary>
    public bool PressKeys(IReadOnlyList<KeyPress> keys);

    public bool ReleaseKeys(IReadOnlyList<KeyPress> keys);

    /// <param name="screenPoint">In Unity's screen pixels, counted from the bottom left.</param>
    public bool MoveMouse(Vector2 screenPoint);

    public bool PressButton(MouseButton button);

    /// <summary>
    /// Moves the mouse with the button held.
    /// </summary>
    /// <param name="delta">In Unity's screen pixels, with up as positive.</param>
    /// <param name="button">The held button.</param>
    public bool DragMouse(Vector2 delta, MouseButton button);

    public bool ReleaseButton(MouseButton button);

    /// <summary>
    /// Lets go of what it holds and reads the player's mouse again. This can't fail. One that throws anyway breaks only
    /// itself: <see cref="InProcessDrivers"/> logs what it threw and still has the drivers after it let go.
    /// </summary>
    public void GiveBack();
}
