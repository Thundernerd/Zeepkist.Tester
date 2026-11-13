using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// How many clicks in a row a press of the Imui driver's is, as Imui's backend counts a player's: presses of a button
/// less than 0.2s and 20 of Imui's pixels from the last.
/// </summary>
public class ImuiClicksTests
{
    // The unit tests' UnityEngine has no method bodies, so not Vector2's constructor either
    private static readonly Vector2 _here = new Vector2 { x = 100, y = 50 };

    private readonly ImuiClicks _clicks = new ImuiClicks();

    [Fact]
    public void APress_IsOneClick()
    {
        Assert.Equal(1, _clicks.Press(MouseButton.Left, _here, 10));
    }

    [Fact]
    public void APressSoonAfterAnother_AtTheSamePlace_IsTheSecondClick()
    {
        _clicks.Press(MouseButton.Left, _here, 10);

        Assert.Equal(2, _clicks.Press(MouseButton.Left, _here, 10.05f));
    }

    [Theory]
    [InlineData(10.25f)]
    [InlineData(11)]
    public void APressAFifthOfASecondOrMoreAfterTheLast_StartsOver(float time)
    {
        _clicks.Press(MouseButton.Left, _here, 10);

        Assert.Equal(1, _clicks.Press(MouseButton.Left, _here, time));
    }

    [Theory]
    [InlineData(120, 50)]
    [InlineData(112, 66)]
    [InlineData(100, 20)]
    public void APress20PixelsOrMoreFromTheLast_StartsOver(float x, float y)
    {
        _clicks.Press(MouseButton.Left, _here, 10);

        Assert.Equal(1, _clicks.Press(MouseButton.Left, new Vector2 { x = x, y = y }, 10.05f));
    }

    [Fact]
    public void APressLessThan20PixelsFromTheLast_IsTheSecondClick()
    {
        _clicks.Press(MouseButton.Left, _here, 10);

        Assert.Equal(2, _clicks.Press(MouseButton.Left, new Vector2 { x = 111, y = 65 }, 10.05f));
    }

    [Fact]
    public void EachButton_CountsItsOwnClicks()
    {
        _clicks.Press(MouseButton.Left, _here, 10);
        _clicks.Press(MouseButton.Right, _here, 10.05f);

        Assert.Equal(2, _clicks.Press(MouseButton.Left, _here, 10.1f));
    }

    [Fact]
    public void APressAfterForgetting_IsOneClick()
    {
        _clicks.Press(MouseButton.Left, _here, 10);

        _clicks.Forget();

        Assert.Equal(1, _clicks.Press(MouseButton.Left, _here, 10.05f));
    }
}
