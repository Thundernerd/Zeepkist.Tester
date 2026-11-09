using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Sends a mouse to uGUI as Unity's own input module sends the player's: what the pointer enters and leaves, what takes a
/// press, and a click when the button goes up over what took it.
/// </summary>
/// <typeparam name="TNode">An object of the scene.</typeparam>
internal sealed class UguiPointer<TNode> : IInputDriver
    where TNode : class
{
    private readonly IUguiScene<TNode> _scene;
    private readonly PointerState<TNode> _state = new PointerState<TNode>();

    /// <summary>
    /// What the pointer is in, from the object it's over up to the top.
    /// </summary>
    private readonly List<TNode> _entered = [];

    public UguiPointer(IUguiScene<TNode> scene)
    {
        _scene = scene;
    }

    /// <summary>
    /// uGUI's pointer events are the mouse's only.
    /// </summary>
    public bool PressKeys(IReadOnlyList<KeyPress> keys)
    {
        return false;
    }

    public bool ReleaseKeys(IReadOnlyList<KeyPress> keys)
    {
        return false;
    }

    public bool MoveMouse(Vector2 screenPoint)
    {
        if (!_scene.Ready)
            return false;

        _state.Position = screenPoint;
        _state.Hit = _scene.HitAt(screenPoint);
        EnterHit();
        return true;
    }

    public bool PressButton(MouseButton button)
    {
        if (!_scene.Ready)
            return false;

        _state.Button = button;
        _state.PressPosition = _state.Position;
        _state.Pressed = Handler(_state.Hit, PointerEvent.Down);
        if (_state.Pressed != null)
            _scene.Send(_state.Pressed, PointerEvent.Down, _state);
        else
            _state.Pressed = Handler(_state.Hit, PointerEvent.Click);

        return true;
    }

    public bool DragMouse(Vector2 delta, MouseButton button)
    {
        // Field by field, because the unit tests' UnityEngine has no method bodies, so not Vector2's operators either
        return MoveMouse(new Vector2 { x = _state.Position.x + delta.x, y = _state.Position.y + delta.y });
    }

    public bool ReleaseButton(MouseButton button)
    {
        if (!_scene.Ready)
            return false;

        _state.Button = button;
        var pressed = _state.Pressed;
        if (pressed != null)
        {
            SendIfHandled(pressed, PointerEvent.Up);
            if (Equals(Handler(_state.Hit, PointerEvent.Click), pressed))
                _scene.Send(pressed, PointerEvent.Click, _state);
        }

        _state.Pressed = null;
        return true;
    }

    /// <summary>
    /// A held button goes up where it is, with no click, because the call that pressed it didn't finish. The pointer then
    /// leaves what it's in, so nothing stays hovered for the player's mouse to find.
    /// </summary>
    public void GiveBack()
    {
        if (_state.Pressed != null && _scene.Ready)
            SendIfHandled(_state.Pressed, PointerEvent.Up);

        _state.Pressed = null;
        _state.Hit = null;
        if (_scene.Ready)
            EnterHit();
        else
            _entered.Clear();
    }

    private void SendIfHandled(TNode node, PointerEvent pointerEvent)
    {
        if (_scene.Handles(node, pointerEvent))
            _scene.Send(node, pointerEvent, _state);
    }

    /// <summary>
    /// The object, or the closest one above it, with a handler for the event, as Unity's <c>ExecuteEvents</c> finds it.
    /// </summary>
    private TNode Handler(TNode node, PointerEvent pointerEvent)
    {
        for (var current = node; current != null; current = _scene.Parent(current))
        {
            if (_scene.Handles(current, pointerEvent))
                return current;
        }

        return null;
    }

    /// <summary>
    /// Leaves what the hit isn't in and enters what it newly is, as Unity's input module does: only below what both share.
    /// </summary>
    private void EnterHit()
    {
        var now = new List<TNode>();
        for (var current = _state.Hit; current != null; current = _scene.Parent(current))
            now.Add(current);

        foreach (var node in _entered)
        {
            if (!now.Contains(node))
                SendIfHandled(node, PointerEvent.Exit);
        }

        foreach (var node in now)
        {
            if (!_entered.Contains(node))
                SendIfHandled(node, PointerEvent.Enter);
        }

        _entered.Clear();
        _entered.AddRange(now);
    }
}
