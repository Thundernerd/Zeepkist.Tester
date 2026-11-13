using System;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Counts presses of a mouse button as clicks in a row, the way Imui's backend counts a player's in
/// <c>OnPointerDown</c>, for the count of the <c>Down</c> event, from which a text field selects a word or a line.
/// </summary>
internal sealed class ImuiClicks
{
    /// <summary>
    /// The backend's <c>MULTI_CLICK_TIME_THRESHOLD</c>.
    /// </summary>
    private const float TimeThreshold = 0.2f;

    /// <summary>
    /// The backend's <c>MULTI_CLICK_POS_THRESHOLD</c>, in Imui's pixels.
    /// </summary>
    private const float PositionThreshold = 20f;

    private readonly int[] _count = new int[3];
    private readonly float[] _time = new float[3];
    private readonly Vector2[] _position = new Vector2[3];

    /// <summary>
    /// How many clicks in a row this press is.
    /// </summary>
    /// <param name="button">The button that goes down.</param>
    /// <param name="position">Where the mouse is, in Imui's pixels.</param>
    /// <param name="time">Seconds of real time.</param>
    public int Press(MouseButton button, Vector2 position, float time)
    {
        var index = (int)button;
        if (time - _time[index] >= TimeThreshold || Distance(position, _position[index]) >= PositionThreshold)
            _count[index] = 0;

        _time[index] = time;
        _position[index] = position;
        return ++_count[index];
    }

    /// <summary>
    /// The next press of each button is one click, whenever and wherever it is.
    /// </summary>
    public void Forget()
    {
        Array.Clear(_count, 0, _count.Length);
    }

    /// <summary>
    /// Field by field, because the unit tests' UnityEngine has no method bodies, so not Vector2's operators either.
    /// </summary>
    private static double Distance(Vector2 a, Vector2 b)
    {
        double x = a.x - b.x;
        double y = a.y - b.y;
        return Math.Sqrt((x * x) + (y * y));
    }
}
