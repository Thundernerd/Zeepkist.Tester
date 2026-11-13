using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Sends a mouse to uGUI as Unity's own input module sends the player's: what the pointer enters and leaves, what takes a
/// press, and a click when the button goes up over what took it. Each call finds what's under the pointer again, because
/// that can change while the pointer stands still, and a press or release goes out before the pointer enters or leaves
/// anything, in the order the input module sends them.
/// </summary>
/// <typeparam name="TNode">An object of the scene.</typeparam>
internal sealed class UguiPointer<TNode> : IInputDriver
    where TNode : class
{
    private readonly IUguiScene<TNode> _scene;
    private readonly PointerState<TNode> _state = new PointerState<TNode>();

    /// <summary>
    /// What took each held button's press, and where the pointer was then, as Unity's input module keeps a
    /// <c>PointerEventData</c> per button: one button going down or up while another is held doesn't touch the other's.
    /// In the order the input module goes through them.
    /// </summary>
    private readonly SortedDictionary<MouseButton, Press> _held = [];

    /// <summary>
    /// What the pointer is in, from the object it's over up to the top.
    /// </summary>
    private readonly List<TNode> _entered = [];

    /// <summary>
    /// Whether a move has put the pointer somewhere since it was last given back.
    /// </summary>
    private bool _placed;

    public UguiPointer(IUguiScene<TNode> scene)
    {
        _scene = scene;
    }

    /// <summary>
    /// Whether there's an event system to send to, and its input module doesn't read Unity's <c>Input</c>. When it does,
    /// as the game's does, the legacy <c>Input</c> driver already moves its pointer, and sending the same mouse here as
    /// well would click everything twice.
    /// </summary>
    private bool Active => _scene.Ready && !_scene.ModuleReadsUnityInput;

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
        if (!Active)
            return false;

        _state.Position = screenPoint;
        _placed = true;
        _state.Hit = _scene.HitAt(screenPoint);
        EnterHit();
        return true;
    }

    public bool PressButton(MouseButton button)
    {
        if (!Active)
            return false;

        FindHit();
        DeselectIfElsewhere();
        var pressed = Handler(_state.Hit, PointerEvent.Down);
        var press = new Press(pressed ?? Handler(_state.Hit, PointerEvent.Click), _state.Position);
        _held[button] = press;
        Use(button, press);
        if (pressed != null)
            _scene.Send(pressed, PointerEvent.Down, _state);

        EnterHit();
        return true;
    }

    public bool DragMouse(Vector2 delta, MouseButton button)
    {
        // Field by field, because the unit tests' UnityEngine has no method bodies, so not Vector2's operators either
        return MoveMouse(new Vector2 { x = _state.Position.x + delta.x, y = _state.Position.y + delta.y });
    }

    public bool ReleaseButton(MouseButton button)
    {
        if (!Active)
            return false;

        FindHit();
        if (_held.TryGetValue(button, out var press))
        {
            _held.Remove(button);
            Use(button, press);
            var pressed = press.Pressed;
            if (pressed != null)
            {
                SendIfHandled(pressed, PointerEvent.Up);
                if (Equals(Handler(_state.Hit, PointerEvent.Click), pressed))
                    _scene.Send(pressed, PointerEvent.Click, _state);
            }
        }

        _state.Pressed = null;
        EnterHit();
        return true;
    }

    /// <summary>
    /// Each held button goes up where it is, with no click, because the call that pressed it didn't finish. The pointer then
    /// leaves what it's in, so nothing stays hovered for the player's mouse to find.
    /// </summary>
    public void GiveBack()
    {
        if (Active)
        {
            foreach (var held in _held)
            {
                Use(held.Key, held.Value);
                if (held.Value.Pressed != null)
                    SendIfHandled(held.Value.Pressed, PointerEvent.Up);
            }
        }

        _held.Clear();
        _state.Pressed = null;
        _state.Hit = null;
        _placed = false;
        if (Active)
            EnterHit();
        else
            _entered.Clear();
    }

    /// <summary>
    /// Selects nothing when the press isn't on what's selected, or on something in it, as the input module's
    /// <c>DeselectIfSelectionChanged</c> does before a press goes down: a click elsewhere takes the focus from an input
    /// field. Whatever the press lands on can then select itself.
    /// </summary>
    private void DeselectIfElsewhere()
    {
        var selected = _scene.Selected;
        if (selected == null)
            return;

        var selectable = SelfAndAbove(_state.Hit).FirstOrDefault(_scene.CanSelect);
        if (!Equals(selectable, selected))
            _scene.Deselect();
    }

    /// <summary>
    /// Has the events that follow carry the button and what took its press.
    /// </summary>
    private void Use(MouseButton button, Press press)
    {
        _state.Button = button;
        _state.Pressed = press.Pressed;
        _state.PressPosition = press.Position;
    }

    /// <summary>
    /// What's under the pointer now, or nothing while it's given back.
    /// </summary>
    private void FindHit()
    {
        _state.Hit = _placed ? _scene.HitAt(_state.Position) : null;
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
        foreach (var current in SelfAndAbove(node))
        {
            if (_scene.Handles(current, pointerEvent))
                return current;
        }

        return null;
    }

    /// <summary>
    /// The object, then each one above it up to the top. None for null.
    /// </summary>
    private IEnumerable<TNode> SelfAndAbove(TNode node)
    {
        for (var current = node; current != null; current = _scene.Parent(current))
            yield return current;
    }

    /// <summary>
    /// Leaves what the hit isn't in and enters what it newly is, as Unity's input module does: only below what both share.
    /// </summary>
    private void EnterHit()
    {
        var now = SelfAndAbove(_state.Hit).ToList();

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

    /// <summary>
    /// A held button: what took its press, or null when nothing did, and where the pointer was as it went down.
    /// </summary>
    private readonly record struct Press(TNode Pressed, Vector2 Position);
}
