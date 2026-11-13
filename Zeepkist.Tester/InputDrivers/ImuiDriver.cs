using System;
using System.Collections.Generic;
using Imui.IO.Events;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Hands keys and a mouse to Imui's backend, which the game's UI and ZeepSDK's windows take their input from, the way
/// its <c>OnGUI</c> and pointer handlers hand it Unity's. The mouse is a position, which Imui reads each frame to find
/// the hovered control, and the button's events, one a frame. A press counts clicks in a row as the backend counts a
/// player's, so two quick clicks in one place are a double-click. A player's press reaches Imui only through uGUI's
/// raycast, so where another uGUI object is on top of the window, or there's nothing at all, Imui gets neither the press
/// nor, while it holds none, a mouse on the control under it. Unity's own <c>Input</c>, and so the level editor, never
/// sees them.
/// </summary>
internal sealed class ImuiDriver : IInputDriver
{
    /// <summary>
    /// Far off the screen, where Imui has no window. Its fields are set one by one because the unit tests' UnityEngine
    /// has no method bodies, so not Vector2's constructor either.
    /// </summary>
    private static readonly Vector2 _away = new Vector2 { x = -100000, y = -100000 };

    private readonly List<KeyPress> _heldKeys = [];
    private readonly HashSet<MouseButton> _heldButtons = [];
    private readonly ImuiClicks _clicks = new ImuiClicks();
    private readonly IImuiBackend _backend;
    private readonly Func<float> _now;

    /// <summary>
    /// Where the tester's pointer is, in screen pixels, or null while it isn't holding one.
    /// </summary>
    private Vector2? _point;

    /// <param name="backend">Imui's backend, which takes the input.</param>
    /// <param name="now">Seconds of real time, for clicks in a row.</param>
    public ImuiDriver(IImuiBackend backend, Func<float> now)
    {
        _backend = backend;
        _now = now;
    }

    /// <summary>
    /// Where Imui finds the mouse, in screen pixels, or null while it reads the player's.
    /// </summary>
    public static Vector2? Position { get; private set; }

    public bool PressKeys(IReadOnlyList<KeyPress> keys)
    {
        if (!_backend.Present)
            return false;

        foreach (var key in keys)
        {
            if (key.Key != KeyCode.None)
                _backend.QueueKey(ImKeyboardEventType.Down, key.Key, key.Modifiers, '\0');

            if (key.Character != '\0')
                _backend.QueueKey(ImKeyboardEventType.Down, KeyCode.None, key.Modifiers, key.Character);
        }

        _heldKeys.AddRange(keys);
        return true;
    }

    public bool ReleaseKeys(IReadOnlyList<KeyPress> keys)
    {
        if (!_backend.Present)
            return false;

        foreach (var key in keys)
        {
            if (key.Key != KeyCode.None)
                _backend.QueueKey(ImKeyboardEventType.Up, key.Key, key.Modifiers, '\0');
        }

        foreach (var key in keys)
            _heldKeys.Remove(key);

        return true;
    }

    public bool MoveMouse(Vector2 screenPoint)
    {
        if (!_backend.Present)
            return false;

        _point = screenPoint;
        return PlaceImuiMouse();
    }

    public bool PressButton(MouseButton button)
    {
        if (!_backend.Present || !TakesPress())
            return false;

        var count = _clicks.Press(button, _backend.MousePosition, _now());
        QueueMouseEvent(ImMouseEventType.Down, button, default, count);

        _heldButtons.Add(button);
        return true;
    }

    /// <summary>
    /// A drag reaches Imui only if the press did, as uGUI sends a player's to what took it, wherever the pointer goes.
    /// Otherwise it's a move.
    /// </summary>
    public bool DragMouse(Vector2 delta, MouseButton button)
    {
        if (!_backend.Present)
            return false;

        // Field by field, because the unit tests' UnityEngine has no method bodies, so not Vector2's operators either
        if (_point is { } point)
            _point = new Vector2 { x = point.x + delta.x, y = point.y + delta.y };

        if (_heldButtons.Contains(button))
        {
            var scale = _backend.Scale;
            QueueMouseEvent(ImMouseEventType.Drag, button, new Vector2 { x = delta.x / scale, y = delta.y / scale });
        }

        return PlaceImuiMouse();
    }

    /// <summary>
    /// Only a press Imui took goes up there. Imui's mouse then stays at the pointer only where it would for a move.
    /// </summary>
    public bool ReleaseButton(MouseButton button)
    {
        if (!_heldButtons.Remove(button))
            return false;

        QueueMouseEvent(ImMouseEventType.Up, button, default);
        PlaceImuiMouse();
        return true;
    }

    public void GiveBack()
    {
        foreach (var button in _heldButtons)
            QueueMouseEvent(ImMouseEventType.Up, button, default);

        _heldButtons.Clear();
        _clicks.Forget();
        ReleaseKeys([.. _heldKeys]);
        _heldKeys.Clear();
        Position = null;
        _point = null;
    }

    /// <summary>
    /// Puts Imui's mouse at the tester's pointer, or, where another uGUI object covers what's there, off every window, so
    /// Imui doesn't find a control there hovered. Imui reads the mouse's position as it draws, not through uGUI. While it
    /// holds a press, its mouse follows the pointer anywhere, as what took the press keeps it.
    /// </summary>
    /// <returns>Whether Imui has the pointer.</returns>
    private bool PlaceImuiMouse()
    {
        if (_heldButtons.Count == 0 && _point is { } point && _backend.TopAt(point) == UguiTop.Other)
        {
            Position = _away;
            return false;
        }

        Position = _point;
        return true;
    }

    /// <summary>
    /// Whether a press at the tester's pointer reaches Imui, as a player's does only where the backend is what uGUI's
    /// raycast finds on top. With no event system to ask, it does, as before uGUI was asked. Where the tester hasn't put
    /// the pointer, Imui finds the player's mouse, and there's no point to ask about.
    /// </summary>
    private bool TakesPress()
    {
        return _point is not { } point || _backend.TopAt(point) is UguiTop.Imui or UguiTop.NoEventSystem;
    }

    private bool QueueMouseEvent(ImMouseEventType type, MouseButton button, Vector2 delta, int count = 1)
    {
        if (!_backend.Present)
            return false;

        _backend.QueueMouse(type, button, delta, count);
        return true;
    }
}
