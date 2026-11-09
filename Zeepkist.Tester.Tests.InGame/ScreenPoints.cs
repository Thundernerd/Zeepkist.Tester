using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Where uGUI draws things, as <c>context.Input</c> takes a point.
/// </summary>
public static class ScreenPoints
{
    /// <summary>
    /// The middle of the rect, in screen pixels counted from the bottom left.
    /// </summary>
    public static Vector2 Center(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }
}
