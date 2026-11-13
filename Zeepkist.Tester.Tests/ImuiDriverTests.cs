using System;
using System.Collections.Generic;
using Imui.IO.Events;
using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Which of the mouse's events the Imui driver hands Imui's backend, by what uGUI finds on top at the pointer: a player's
/// press reaches Imui only through uGUI's raycast, so only where the window is the topmost thing there.
/// </summary>
public class ImuiDriverTests
{
    // The unit tests' UnityEngine has no method bodies, so not Vector2's constructor either
    private static readonly Vector2 _onWindow = new Vector2 { x = 10, y = 10 };
    private static readonly Vector2 _underUgui = new Vector2 { x = 50, y = 10 };

    private readonly FakeBackend _backend = new FakeBackend();
    private readonly ImuiDriver _driver;

    public ImuiDriverTests()
    {
        _backend.Top(_onWindow, UguiTop.Imui);
        _backend.Top(_underUgui, UguiTop.Other);
        _driver = new ImuiDriver(_backend, () => 0);
    }

    [Fact]
    public void AClickWhereTheWindowIsOnTop_ReachesImui()
    {
        Assert.True(_driver.MoveMouse(_onWindow));
        Assert.True(_driver.PressButton(MouseButton.Left));
        Assert.True(_driver.ReleaseButton(MouseButton.Left));

        Assert.Equal(["Down Left 1", "Up Left"], _backend.Queued);
        _driver.GiveBack();
    }

    [Fact]
    public void AClickWhereAnotherUguiObjectIsOnTop_DoesntReachImui()
    {
        _driver.MoveMouse(_underUgui);

        Assert.False(_driver.PressButton(MouseButton.Left));
        Assert.False(_driver.ReleaseButton(MouseButton.Left));
        _driver.GiveBack();

        Assert.Empty(_backend.Queued);
    }

    [Fact]
    public void MovingWhereAnotherUguiObjectIsOnTop_DoesntPutImuisMouseOnWhatsUnderIt()
    {
        Assert.False(_driver.MoveMouse(_underUgui));

        AssertImuiMouseNotAt(_underUgui);
        _driver.GiveBack();
    }

    [Fact]
    public void ADragFromWhereAnotherUguiObjectIsOnTop_DoesntReachImui_EvenOntoTheWindow()
    {
        _driver.MoveMouse(_underUgui);
        _driver.PressButton(MouseButton.Left);

        Assert.True(_driver.DragMouse(Delta(_underUgui, _onWindow), MouseButton.Left));
        _driver.ReleaseButton(MouseButton.Left);

        Assert.Empty(_backend.Queued);
        AssertImuiMouseAt(_onWindow);
        _driver.GiveBack();
    }

    [Fact]
    public void ADragFromTheWindow_StaysWithImui_EvenUnderAnotherUguiObject()
    {
        _driver.MoveMouse(_onWindow);
        _driver.PressButton(MouseButton.Left);

        Assert.True(_driver.DragMouse(Delta(_onWindow, _underUgui), MouseButton.Left));
        AssertImuiMouseAt(_underUgui);
        _driver.ReleaseButton(MouseButton.Left);

        Assert.Equal(["Down Left 1", "Drag Left 40,0", "Up Left"], _backend.Queued);
        _driver.GiveBack();
    }

    [Fact]
    public void WhileImuiHoldsAPress_ItsMouseFollowsThePointer_EvenUnderAnotherUguiObject()
    {
        _driver.MoveMouse(_onWindow);
        _driver.PressButton(MouseButton.Left);

        Assert.True(_driver.MoveMouse(_underUgui));

        AssertImuiMouseAt(_underUgui);
        _driver.GiveBack();
    }

    [Fact]
    public void LettingGoUnderAnotherUguiObject_TakesImuisMouseOffWhatsUnderIt()
    {
        _driver.MoveMouse(_onWindow);
        _driver.PressButton(MouseButton.Left);
        _driver.DragMouse(Delta(_onWindow, _underUgui), MouseButton.Left);

        _driver.ReleaseButton(MouseButton.Left);

        AssertImuiMouseNotAt(_underUgui);
        _driver.GiveBack();
    }

    // UguiTop is internal, so a public test takes its name
    [Theory]
    [InlineData(nameof(UguiTop.Imui), true)]
    [InlineData(nameof(UguiTop.Other), false)]
    [InlineData(nameof(UguiTop.Nothing), false)]
    [InlineData(nameof(UguiTop.NoEventSystem), true)]
    public void APress_ReachesImuiOnlyWhereTheWindowIsOnTop_OrWhereNoEventSystemCanSay(string top, bool taken)
    {
        var point = new Vector2 { x = 90, y = 10 };
        _backend.Top(point, Enum.Parse<UguiTop>(top));
        _driver.MoveMouse(point);

        Assert.Equal(taken, _driver.PressButton(MouseButton.Left));
        _driver.GiveBack();
    }

    /// <summary>
    /// Vector2's operators have no body here either.
    /// </summary>
    private static Vector2 Delta(Vector2 from, Vector2 to)
    {
        return new Vector2 { x = to.x - from.x, y = to.y - from.y };
    }

    /// <summary>
    /// Where Imui finds the mouse, field by field, as Vector2's own equality has no body here either.
    /// </summary>
    private static void AssertImuiMouseAt(Vector2 screenPoint)
    {
        Assert.NotNull(ImuiDriver.Position);
        Assert.Equal((screenPoint.x, screenPoint.y), (ImuiDriver.Position.Value.x, ImuiDriver.Position.Value.y));
    }

    /// <summary>
    /// That Imui has a mouse from the tester, but not at the point.
    /// </summary>
    private static void AssertImuiMouseNotAt(Vector2 screenPoint)
    {
        Assert.NotNull(ImuiDriver.Position);
        Assert.NotEqual((screenPoint.x, screenPoint.y), (ImuiDriver.Position.Value.x, ImuiDriver.Position.Value.y));
    }

    private sealed class FakeBackend : IImuiBackend
    {
        // Vector2's own equality has no body here either
        private readonly Dictionary<(float X, float Y), UguiTop> _tops = [];

        public List<string> Queued { get; } = [];

        public bool Present { get; set; } = true;

        public Vector2 MousePosition => default;

        public float Scale => 1;

        public void Top(Vector2 screenPoint, UguiTop top)
        {
            _tops[(screenPoint.x, screenPoint.y)] = top;
        }

        public UguiTop TopAt(Vector2 screenPoint)
        {
            return _tops.TryGetValue((screenPoint.x, screenPoint.y), out var top) ? top : UguiTop.Nothing;
        }

        public void QueueKey(ImKeyboardEventType type, KeyCode key, EventModifiers modifiers, char character)
        {
            Queued.Add($"{type} {key} {character}");
        }

        public void QueueMouse(ImMouseEventType type, MouseButton button, Vector2 delta, int count)
        {
            Queued.Add(type switch
            {
                ImMouseEventType.Down => $"Down {button} {count}",
                ImMouseEventType.Drag => $"Drag {button} {delta.x},{delta.y}",
                _ => $"{type} {button}",
            });
        }
    }
}
