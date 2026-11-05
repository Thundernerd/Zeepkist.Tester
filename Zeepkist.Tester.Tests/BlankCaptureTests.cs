using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class BlankCaptureTests
{
    [Fact]
    public void ACapture_WhereEveryPixelIsTheSame_IsBlank()
    {
        byte[] transparent = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Assert.True(BlankCapture.IsBlank(transparent, 4, out var color));
        Assert.Equal("#00000000", color);
    }

    [Fact]
    public void ACapture_WithOnePixelThatDiffers_IsNotBlank()
    {
        byte[] sky = [90, 160, 220, 255, 90, 160, 220, 255, 90, 160, 221, 255];

        Assert.False(BlankCapture.IsBlank(sky, 4, out _));
    }

    [Fact]
    public void ACapture_ThatDiffersOnlyInAlpha_IsNotBlank()
    {
        byte[] pixels = [0, 0, 0, 0, 0, 0, 0, 255];

        Assert.False(BlankCapture.IsBlank(pixels, 4, out _));
    }

    [Fact]
    public void ThePixelsColor_IsWrittenInTheCapturesByteOrder()
    {
        byte[] pixels = [255, 0, 128, 255, 255, 0, 128, 255];

        Assert.True(BlankCapture.IsBlank(pixels, 4, out var color));
        Assert.Equal("#FF0080FF", color);
    }

    [Fact]
    public void ACaptureWithThreeBytesAPixel_IsReadByThree()
    {
        // As four bytes a pixel, the first would be 1 2 3 1 and the second 2 3 1 2
        byte[] pixels = [1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3];

        Assert.True(BlankCapture.IsBlank(pixels, 3, out var color));
        Assert.Equal("#010203", color);
    }
}
