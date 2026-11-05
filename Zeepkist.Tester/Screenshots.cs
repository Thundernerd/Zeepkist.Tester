using System.Collections;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// Saves the run's screenshots into its results, and marks a blank one there, so a run whose captures show nothing
/// says so.
/// </summary>
internal sealed class Screenshots
{
    private readonly RunResults _results;
    private readonly ManualLogSource _logger;

    public Screenshots(RunResults results, ManualLogSource logger)
    {
        _results = results;
        _logger = logger;
    }

    /// <summary>
    /// The color of every pixel of the last screenshot when they were all the same, or null.
    /// </summary>
    public string LastBlankColor { get; private set; }

    /// <summary>
    /// With <paramref name="cover"/>, fills the frame with it just before the capture, so the screenshot is blank.
    /// </summary>
    public IEnumerator Take(string name, Color? cover = null)
    {
        yield return new WaitForEndOfFrame();
        if (cover is { } color)
            GL.Clear(true, true, color);

        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        bool blank;
        string blankColor;
        try
        {
            blank = BlankCapture.IsBlank(texture.GetRawTextureData(), (int)GraphicsFormatUtility.GetBlockSize(texture.graphicsFormat), out blankColor);
            File.WriteAllBytes(_results.ScreenshotPath(name), texture.EncodeToPNG());
        }
        finally
        {
            Object.Destroy(texture);
        }

        _results.AddScreenshot(name);
        LastBlankColor = blank ? blankColor : null;
        if (blank)
        {
            _results.MarkBlank(name, blankColor);
            _logger.LogWarning($"{TestRun.Marker} screenshot {name} is blank: every pixel is {blankColor}");
        }
        else
        {
            _logger.LogInfo($"{TestRun.Marker} screenshot {name}");
        }
    }
}
