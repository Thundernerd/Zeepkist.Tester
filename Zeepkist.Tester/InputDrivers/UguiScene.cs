using System.Collections.Generic;
using Imui.IO.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// The game's uGUI, which its menus are made of: what's under a point is what the event system's raycasters find there,
/// and events go to an object's handlers through <see cref="ExecuteEvents"/>, as Unity's own input module sends them.
/// Imui's windows are a uGUI graphic too, which <see cref="ImuiDriver"/> hands the mouse to, so a point over one finds
/// nothing here: it doesn't get the mouse twice, and what's behind the window gets none of it, as with the player's.
/// </summary>
internal sealed class UguiScene : IUguiScene<GameObject>
{
    private readonly List<RaycastResult> _results = [];
    /// <summary>
    /// What the last <see cref="HitAt"/> found, which is what the pointer is over now.
    /// </summary>
    private RaycastResult _lastHit;

    /// <summary>
    /// What the pointer was over as its button last went down.
    /// </summary>
    private RaycastResult _pressHit;

    public bool Ready => EventSystem.current != null;

    public GameObject HitAt(Vector2 screenPoint)
    {
        _results.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPoint }, _results);
        _lastHit = default;
        foreach (var result in _results)
        {
            // Sorted topmost first, as the input module takes them
            if (result.gameObject != null)
            {
                _lastHit = result;
                break;
            }
        }

        if (_lastHit.gameObject != null && _lastHit.gameObject.GetComponent<ImuiUnityGUIBackend>() != null)
            _lastHit = default;

        return _lastHit.gameObject;
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
            _pressHit = _lastHit;

        var data = new PointerEventData(EventSystem.current)
        {
            position = state.Position,
            button = (PointerEventData.InputButton)state.Button,
            pointerCurrentRaycast = _lastHit,
            pointerEnter = state.Hit,
            pointerPress = state.Pressed,
            rawPointerPress = state.Pressed,
            pressPosition = state.PressPosition,
            pointerPressRaycast = _pressHit,
            eligibleForClick = state.Pressed != null,
            clickCount = 1,
            clickTime = Time.unscaledTime,
            useDragThreshold = true,
        };

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
}
