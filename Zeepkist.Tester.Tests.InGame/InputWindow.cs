using Imui.Controls;
using Imui.Core;
using Imui.IO.UGUI;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// A ZeepSDK window with a text field and a slider, for the steps to reach with <c>context.Input</c> the way a player does.
/// </summary>
public sealed class InputWindow : IZeepGUIDrawer
{
    private string _text = string.Empty;
    private ImTextEditState _fieldState;
    private ImRect _fieldRect;
    private ImRect _sliderRect;

    public bool Open { get; set; }

    public bool Drawn { get; private set; }

    public string Text => _text;

    public bool FieldFocused { get; private set; }

    /// <summary>
    /// What's selected in the field, nothing when its caret selects nothing.
    /// </summary>
    public string SelectedText
    {
        get
        {
            var start = Mathf.Clamp(Mathf.Min(_fieldState.Caret, _fieldState.Caret + _fieldState.Selection), 0, _text.Length);
            var end = Mathf.Clamp(Mathf.Max(_fieldState.Caret, _fieldState.Caret + _fieldState.Selection), 0, _text.Length);
            return _text.Substring(start, end - start);
        }
    }

    public float Slider { get; private set; }

    public Vector2 FieldCenter => ToScreen(_fieldRect.Center);

    /// <summary>
    /// Just inside the field's left end, on the first word of its text.
    /// </summary>
    public Vector2 FieldStart => ToScreen(new Vector2(_fieldRect.X + 8, _fieldRect.Center.y));

    public Vector2 SliderLeft => ToScreen(SliderLeftInImui);

    public Vector2 SliderLeftInImui => new Vector2(_sliderRect.X + 4, _sliderRect.Center.y);

    /// <summary>
    /// How many Imui backends there are, which should be the one the driver finds.
    /// </summary>
    public int Backends => Object.FindObjectsOfType<ImuiUnityGUIBackend>().Length;

    public int SliderWidth => Mathf.RoundToInt(_sliderRect.W * Backend.GetScale());

    /// <summary>
    /// Where Imui finds the mouse, in Imui's pixels.
    /// </summary>
    public Vector2 ImuiMouse => Backend.GetMousePosition();

    /// <summary>
    /// Where the player's mouse is, in Imui's pixels.
    /// </summary>
    public Vector2 PlayerMouse => (Vector2)(UnityEngine.Input.mousePosition - (Vector3)Backend.GetWorldRect().position) / Backend.GetScale();

    /// <summary>
    /// Whether Imui finds the mouse at the slider's left end, where <see cref="SliderLeft"/> moves it.
    /// </summary>
    public bool ImuiMouseAtSliderLeft => Vector2.Distance(ImuiMouse, SliderLeftInImui) <= 0.5f;

    /// <summary>
    /// Whether Imui reads the player's mouse, and not one <c>context.Input</c> holds.
    /// </summary>
    public bool ImuiReadsPlayerMouse => ImuiMouse == PlayerMouse;

    private static ImuiUnityGUIBackend Backend => Object.FindObjectOfType<ImuiUnityGUIBackend>();

    public void OnZeepGUI(ImGui gui)
    {
        if (!Open)
        {
            Drawn = false;
            return;
        }

        gui.BeginWindow("Tester input", new ImSize(400, 200));
        _fieldRect = gui.AddSingleRowRect(default);
        var id = gui.GetNextControlId();
        ref var state = ref gui.Storage.Get<ImTextEditState>(id);
        gui.TextEdit(id, ref _text, ref state, _fieldRect, false);
        FieldFocused = gui.IsControlActive(id);
        _fieldState = state;

        _sliderRect = gui.AddSingleRowRect(default);
        var slider = Slider;
        gui.Slider(ref slider, 0, 100, _sliderRect);
        Slider = slider;
        gui.EndWindow();
        Drawn = true;
    }

    /// <summary>
    /// Imui draws in its own pixels, and <c>context.Input</c> takes the screen's.
    /// </summary>
    private static Vector2 ToScreen(Vector2 imuiPoint)
    {
        var backend = Backend;
        return (imuiPoint * backend.GetScale()) + backend.GetWorldRect().position;
    }
}
