using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Sends the game keys and a mouse. A step yields what these return, and the input is handled by the time that ends. Each
/// fails the step when it can't send its input, with a <see cref="StepFailedException"/>.
/// </summary>
public interface IInput
{
    /// <summary>
    /// How the next input reaches the game. Each scenario starts on the route of its <c>input:</c> header, whatever a
    /// step switched it to in the scenario before.
    /// </summary>
    public InputRoute Route { get; set; }

    /// <summary>
    /// Presses the keys in one frame and lets go in the next, which is when they've been handled. Each key is a key event,
    /// then the character it types as an event of its own, and every Escape comes in that frame, as a quick double press does.
    /// </summary>
    public IEnumerator Press(Keys keys);

    /// <summary>
    /// Types the text one key a frame. A character without a key on a US keyboard comes as its character alone.
    /// </summary>
    public IEnumerator Type(string text);

    /// <summary>
    /// Presses the keys in one frame and lets go of them when <paramref name="duration"/> is over.
    /// </summary>
    public IEnumerator Hold(Keys keys, Seconds duration);

    /// <summary>
    /// Moves the mouse to a point on the screen, in Unity's screen pixels, counted from the bottom left. It stays there
    /// for the steps after it, until <see cref="GiveBack"/>.
    /// </summary>
    public IEnumerator MoveMouse(Vector2 screenPoint);

    /// <summary>
    /// Moves the mouse to the point, then presses and lets go of the button, each in a frame of its own.
    /// </summary>
    public IEnumerator Click(Vector2 screenPoint, MouseButton button = MouseButton.Left);

    /// <summary>
    /// Moves the mouse to <paramref name="from"/>, presses the button, makes each move in turn and lets go. The mouse
    /// stays where the moves took it.
    /// </summary>
    public IEnumerator Drag(Vector2 from, IReadOnlyList<DragMove> moves, MouseButton button = MouseButton.Left);

    /// <summary>
    /// Lets go of what's held and hands back the player's mouse, on every route. It takes effect from the next frame and
    /// can always be called, with nothing held or with nothing in the game to take it. It leaves <see cref="Route"/> alone.
    /// </summary>
    public void GiveBack();
}
