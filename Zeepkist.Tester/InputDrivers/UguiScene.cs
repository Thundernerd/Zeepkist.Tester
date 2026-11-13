using System.Collections.Generic;
using Imui.IO.UGUI;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// The game's uGUI, which its menus are made of: what's under a point is what the event system's raycasters find there,
/// and events go to an object's handlers through <see cref="ExecuteEvents"/>, as Unity's own input module sends them.
/// Imui's windows are a uGUI graphic too, which <see cref="ImuiDriver"/> hands the mouse to, so a point over one finds
/// nothing here: it doesn't get the mouse twice, and what's behind the window gets none of it, as with the player's.
/// The Imui driver asks the same raycast, through <see cref="TopAt"/>, whether the window is on top at its pointer.
/// </summary>
internal sealed class UguiScene : IUguiScene<GameObject>
{
    private readonly List<RaycastResult> _results = [];

    /// <summary>
    /// What the last <see cref="HitAt"/> found, which is what the pointer is over now.
    /// </summary>
    private RaycastResult _lastHit;

    /// <summary>
    /// What the pointer was over as each button last went down.
    /// </summary>
    private readonly Dictionary<MouseButton, RaycastResult> _pressHits = [];

    public bool Ready => EventSystem.current != null;

    /// <summary>
    /// Unity's own pointer modules read the mouse through <see cref="BaseInput"/>, which reads <c>Input</c>, unless
    /// something overrides it. A module of another kind, such as Rewired's, reads its own.
    /// </summary>
    public bool ModuleReadsUnityInput => EventSystem.current != null
        && EventSystem.current.currentInputModule is StandaloneInputModule module
        && module.input != null
        && module.input.GetType() == typeof(BaseInput);

    public GameObject Selected => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

    public bool CanSelect(GameObject node)
    {
        return node != null && ExecuteEvents.CanHandleEvent<ISelectHandler>(node);
    }

    /// <summary>
    /// Passes the press's pointer data, as the input module does, for what reads the pointer as it's deselected. The
    /// press goes down where the pointer is now, so that's its raycast.
    /// </summary>
    public void Deselect(PointerState<GameObject> state)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null, Data(state, _lastHit));
    }

    public GameObject HitAt(Vector2 screenPoint)
    {
        _lastHit = Topmost(screenPoint);
        if (IsImui(_lastHit.gameObject))
            _lastHit = default;

        return _lastHit.gameObject;
    }

    /// <summary>
    /// What's on top at the point for <see cref="ImuiDriver"/>, from the same raycast as <see cref="HitAt"/>.
    /// </summary>
    /// <param name="screenPoint">In Unity's screen pixels, counted from the bottom left.</param>
    public UguiTop TopAt(Vector2 screenPoint)
    {
        if (!Ready)
            return UguiTop.NoEventSystem;

        var hit = Topmost(screenPoint).gameObject;
        if (hit == null)
            return UguiTop.Nothing;

        return IsImui(hit) ? UguiTop.Imui : UguiTop.Other;
    }

    public GameObject Parent(GameObject node)
    {
        if (node == null)
            return null;

        var parent = node.transform.parent;
        return parent != null ? parent.gameObject : null;
    }

    /// <summary>
    /// False for an object that's been destroyed, such as by the click before.
    /// </summary>
    public bool Handles(GameObject node, PointerEvent pointerEvent)
    {
        if (node == null)
            return false;

        return pointerEvent switch
        {
            PointerEvent.Enter => ExecuteEvents.CanHandleEvent<IPointerEnterHandler>(node),
            PointerEvent.Exit => ExecuteEvents.CanHandleEvent<IPointerExitHandler>(node),
            PointerEvent.Down => ExecuteEvents.CanHandleEvent<IPointerDownHandler>(node),
            PointerEvent.Up => ExecuteEvents.CanHandleEvent<IPointerUpHandler>(node),
            PointerEvent.Click => ExecuteEvents.CanHandleEvent<IPointerClickHandler>(node),
            _ => false,
        };
    }

    public void Send(GameObject node, PointerEvent pointerEvent, PointerState<GameObject> state)
    {
        if (node == null || EventSystem.current == null)
            return;

        if (pointerEvent == PointerEvent.Down)
            _pressHits[state.Button] = _lastHit;

        _pressHits.TryGetValue(state.Button, out var pressHit);
        var data = Data(state, pressHit);
        switch (pointerEvent)
        {
            case PointerEvent.Enter:
                ExecuteEvents.Execute(node, data, ExecuteEvents.pointerEnterHandler);
                break;
            case PointerEvent.Exit:
                ExecuteEvents.Execute(node, data, ExecuteEvents.pointerExitHandler);
                break;
            case PointerEvent.Down:
                ExecuteEvents.Execute(node, data, ExecuteEvents.pointerDownHandler);
                break;
            case PointerEvent.Up:
                ExecuteEvents.Execute(node, data, ExecuteEvents.pointerUpHandler);
                break;
            case PointerEvent.Click:
                ExecuteEvents.Execute(node, data, ExecuteEvents.pointerClickHandler);
                break;
        }
    }

    /// <summary>
    /// The topmost of what the event system's raycasters find at the point, as the input module takes it, or nothing.
    /// </summary>
    private RaycastResult Topmost(Vector2 screenPoint)
    {
        _results.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPoint }, _results);
        foreach (var result in _results)
        {
            // Sorted topmost first
            if (result.gameObject != null)
                return result;
        }

        return default;
    }

    private static bool IsImui(GameObject node)
    {
        return node != null && node.GetComponent<ImuiUnityGUIBackend>() != null;
    }

    private PointerEventData Data(PointerState<GameObject> state, RaycastResult pressHit)
    {
        return new PointerEventData(EventSystem.current)
        {
            position = state.Position,
            button = (PointerEventData.InputButton)state.Button,
            pointerCurrentRaycast = _lastHit,
            pointerEnter = state.Hit,
            pointerPress = state.Pressed,
            rawPointerPress = state.Pressed,
            pressPosition = state.PressPosition,
            pointerPressRaycast = pressHit,
            eligibleForClick = state.Pressed != null,
            clickCount = 1,
            clickTime = Time.unscaledTime,
            useDragThreshold = true,
        };
    }
}
