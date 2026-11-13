using Imui.IO.Events;
using Imui.IO.UGUI;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// The game's one <see cref="ImuiUnityGUIBackend"/>, which the game's UI and ZeepSDK's windows take their input from,
/// and the game's uGUI above it, as <see cref="UguiScene"/> raycasts it.
/// </summary>
internal sealed class ImuiBackend : IImuiBackend
{
    private ImuiUnityGUIBackend _backend;

    public bool Present => Backend != null;

    public Vector2 MousePosition => Backend.GetMousePosition();

    public float Scale => Backend.GetScale();

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

    public UguiTop TopAt(Vector2 screenPoint)
    {
        return UguiScene.TopAt(screenPoint);
    }

    public void QueueKey(ImKeyboardEventType type, KeyCode key, EventModifiers modifiers, char character)
    {
        Backend.nextKeyboardEvents.PushFront(new ImKeyboardEvent(type, key, modifiers, character));
    }

    public void QueueMouse(ImMouseEventType type, MouseButton button, Vector2 delta, int count)
    {
        Backend.mouseEventsQueue.PushFront(new ImMouseEvent(type, (int)button, EventModifiers.None, delta, ImMouseDevice.Mouse, count));
    }
}
