using System.Collections.Generic;
using Imui.IO.Events;
using Imui.IO.UGUI;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Hands keys and a mouse to Imui's backend, which the game's UI and ZeepSDK's windows take their input from, the way
/// its <c>OnGUI</c> and pointer handlers hand it Unity's. The mouse is a position, which Imui reads each frame to find
/// the hovered control, and the button's events, one a frame. Unity's own <c>Input</c>, and so the level editor, never
/// sees them.
/// </summary>
internal sealed class ImuiDriver : IInputDriver
{
    private readonly List<KeyPress> _heldKeys = [];
    private readonly HashSet<MouseButton> _heldButtons = [];
    private ImuiUnityGUIBackend _backend;

    /// <summary>
    /// Where Imui finds the mouse, in screen pixels, or null while it reads the player's.
    /// </summary>
    public static Vector2? Position { get; private set; }

    /// <summary>
    /// Null when there's none.
    /// </summary>
    private ImuiUnityGUIBackend Backend
    {
        get
        {
            if (_backend == null)
                _backend = Object.FindObjectOfType<ImuiUnityGUIBackend>();

            return _backend;
        }
    }

    public bool PressKeys(IReadOnlyList<KeyPress> keys)
    {
        var backend = Backend;
        if (backend == null)
            return false;

        foreach (var key in keys)
        {
            if (key.Key != KeyCode.None)
                backend.nextKeyboardEvents.PushFront(new ImKeyboardEvent(ImKeyboardEventType.Down, key.Key, key.Modifiers, '\0'));

            if (key.Character != '\0')
                backend.nextKeyboardEvents.PushFront(new ImKeyboardEvent(ImKeyboardEventType.Down, KeyCode.None, key.Modifiers, key.Character));
        }

        _heldKeys.AddRange(keys);
        return true;
    }

    public bool ReleaseKeys(IReadOnlyList<KeyPress> keys)
    {
        var backend = Backend;
        if (backend == null)
            return false;

        foreach (var key in keys)
        {
            if (key.Key != KeyCode.None)
                backend.nextKeyboardEvents.PushFront(new ImKeyboardEvent(ImKeyboardEventType.Up, key.Key, key.Modifiers, '\0'));
        }

        foreach (var key in keys)
            _heldKeys.Remove(key);

        return true;
    }

    public bool MoveMouse(Vector2 screenPoint)
    {
        if (Backend == null)
            return false;

        Position = screenPoint;
        return true;
    }

    public bool PressButton(MouseButton button)
    {
        if (!QueueMouseEvent(ImMouseEventType.Down, button, Vector2.zero))
            return false;

        _heldButtons.Add(button);
        return true;
    }

    public bool DragMouse(Vector2 delta, MouseButton button)
    {
        var backend = Backend;
        if (backend == null)
            return false;

        QueueMouseEvent(ImMouseEventType.Drag, button, delta / backend.GetScale());

        Position += delta;
        return true;
    }

    public bool ReleaseButton(MouseButton button)
    {
        _heldButtons.Remove(button);
        return QueueMouseEvent(ImMouseEventType.Up, button, Vector2.zero);
    }

    public void GiveBack()
    {
        foreach (var button in _heldButtons)
            QueueMouseEvent(ImMouseEventType.Up, button, Vector2.zero);

        _heldButtons.Clear();
        ReleaseKeys([.. _heldKeys]);
        _heldKeys.Clear();
        Position = null;
    }

    private bool QueueMouseEvent(ImMouseEventType type, MouseButton button, Vector2 delta)
    {
        var backend = Backend;
        if (backend == null)
            return false;

        backend.mouseEventsQueue.PushFront(new ImMouseEvent(type, (int)button, EventModifiers.None, delta, ImMouseDevice.Mouse));
        return true;
    }
}
