using UnityEngine;
using UnityEngine.UI;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// A uGUI button of the plugin's own, on a canvas above the game's, for the steps to click with <c>context.Input</c>.
/// </summary>
public sealed class UguiButton
{
    private GameObject _canvas;
    private RectTransform _button;

    public int Clicks { get; private set; }

    /// <summary>
    /// In screen pixels, counted from the bottom left.
    /// </summary>
    public Vector2 Center => ScreenPoints.Center(_button);

    public int Width => Mathf.RoundToInt(_button.rect.width);

    public void Show()
    {
        Hide();
        Clicks = 0;
        _canvas = new GameObject("Tester uGUI button", typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        _button = button.GetComponent<RectTransform>();
        _button.SetParent(_canvas.transform, false);
        _button.sizeDelta = new Vector2(200, 80);
        button.GetComponent<Button>().onClick.AddListener(() => Clicks++);
    }

    /// <summary>
    /// Centres the button on the point, in screen pixels counted from the bottom left, which is where its overlay canvas
    /// draws it.
    /// </summary>
    public void MoveTo(Vector2 screenPoint)
    {
        _button.position = screenPoint;
    }

    public void Hide()
    {
        if (_canvas != null)
            Object.Destroy(_canvas);

        _canvas = null;
    }
}
