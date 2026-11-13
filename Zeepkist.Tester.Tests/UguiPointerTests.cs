using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Which pointer events the uGUI driver sends to which objects, as Unity's own input module does for a player's mouse.
/// Objects are paths, such as <c>menu/button/label</c>, whose parent is the path without its last part.
/// </summary>
public class UguiPointerTests
{
    // The unit tests' UnityEngine has no method bodies, so not Vector2's constructor either
    private static readonly Vector2 _onButton = new Vector2 { x = 10, y = 10 };
    private static readonly Vector2 _onOther = new Vector2 { x = 50, y = 10 };
    private static readonly Vector2 _onNothing = new Vector2 { x = 90, y = 10 };

    private readonly FakeScene _scene = new FakeScene();
    private readonly UguiPointer<string> _pointer;

    public UguiPointerTests()
    {
        _scene.Hit(_onButton, "menu/button/label");
        _scene.Hit(_onOther, "menu/other");
        PointerEvent[] all = [PointerEvent.Enter, PointerEvent.Exit, PointerEvent.Down, PointerEvent.Up, PointerEvent.Click];
        _scene.Handlers["menu/button"] = all;
        _scene.Handlers["menu/other"] = all;
        _pointer = new UguiPointer<string>(_scene);
    }

    [Fact]
    public void AClick_EntersPressesAndClicksWhatIsUnderIt()
    {
        Assert.True(_pointer.MoveMouse(_onButton));
        Assert.True(_pointer.PressButton(MouseButton.Left));
        Assert.True(_pointer.ReleaseButton(MouseButton.Left));

        Assert.Equal(
        [
            "Enter menu/button",
            "Down menu/button Left", "Up menu/button Left", "Click menu/button Left",
        ], _scene.Sent);
    }

    [Fact]
    public void AReleaseOverSomethingElse_LetsGoOfWhatTookThePress_WithoutAClick()
    {
        _pointer.MoveMouse(_onButton);
        _pointer.PressButton(MouseButton.Left);
        _pointer.DragMouse(Delta(_onButton, _onOther), MouseButton.Left);
        _scene.Sent.Clear();

        _pointer.ReleaseButton(MouseButton.Left);

        Assert.Equal(["Up menu/button Left"], _scene.Sent);
    }

    [Fact]
    public void TwoButtonsHeldAtOnce_EachGoUpOnWhatTookTheirOwnPress_WithAClickOnlyWhereReleasedOverThat()
    {
        _pointer.MoveMouse(_onButton);
        _pointer.PressButton(MouseButton.Left);
        _pointer.DragMouse(Delta(_onButton, _onOther), MouseButton.Left);
        _pointer.PressButton(MouseButton.Right);
        _pointer.ReleaseButton(MouseButton.Right);
        _scene.Sent.Clear();

        _pointer.ReleaseButton(MouseButton.Left);

        Assert.Equal(["Up menu/button Left"], _scene.Sent);
    }

    [Fact]
    public void ASecondButtonPressedAndReleasedWhileTheFirstIsHeld_ClicksWhatItPressed()
    {
        _pointer.MoveMouse(_onButton);
        _pointer.PressButton(MouseButton.Left);
        _pointer.DragMouse(Delta(_onButton, _onOther), MouseButton.Left);
        _scene.Sent.Clear();

        _pointer.PressButton(MouseButton.Right);
        _pointer.ReleaseButton(MouseButton.Right);

        Assert.Equal(["Down menu/other Right", "Up menu/other Right", "Click menu/other Right"], _scene.Sent);
    }

    [Fact]
    public void GiveBack_LetsGoOfEveryHeldButton()
    {
        _pointer.MoveMouse(_onButton);
        _pointer.PressButton(MouseButton.Left);
        _pointer.DragMouse(Delta(_onButton, _onOther), MouseButton.Left);
        _pointer.PressButton(MouseButton.Right);
        _scene.Sent.Clear();

        _pointer.GiveBack();

        Assert.Equal(["Up menu/button Left", "Up menu/other Right", "Exit menu/other"], _scene.Sent);
    }

    [Fact]
    public void APressOverSomethingElse_DeselectsWhatIsSelected_BeforeThePressGoesDown()
    {
        _scene.Selectable.Add("menu/field");
        _scene.Selected = "menu/field";
        _pointer.MoveMouse(_onButton);
        _scene.Sent.Clear();

        _pointer.PressButton(MouseButton.Left);

        Assert.Null(_scene.Selected);
        Assert.Equal(["Deselect menu/field", "Down menu/button Left"], _scene.Sent);
    }

    [Fact]
    public void APressOverWhatIsSelected_KeepsItSelected()
    {
        _scene.Selectable.Add("menu/button");
        _scene.Selected = "menu/button";
        _pointer.MoveMouse(_onButton);

        _pointer.PressButton(MouseButton.Left);

        Assert.Equal("menu/button", _scene.Selected);
    }

    [Fact]
    public void MovingToSomethingElse_LeavesWhatItWasIn_AndEntersWhatItNowIs_ButNotWhatBothShare()
    {
        _scene.Handlers["menu"] = [PointerEvent.Enter, PointerEvent.Exit];
        _pointer.MoveMouse(_onButton);
        Assert.Equal(["Enter menu/button", "Enter menu"], _scene.Sent);
        _scene.Sent.Clear();

        _pointer.MoveMouse(_onOther);
        Assert.Equal(["Exit menu/button", "Enter menu/other"], _scene.Sent);
        _scene.Sent.Clear();

        _pointer.MoveMouse(_onNothing);
        Assert.Equal(["Exit menu/other", "Exit menu"], _scene.Sent);
    }

    [Fact]
    public void GiveBack_LetsGoOfAHeldButtonWithoutAClick_AndLeavesWhatThePointerIsIn()
    {
        _pointer.MoveMouse(_onButton);
        _pointer.PressButton(MouseButton.Right);
        _scene.Sent.Clear();

        _pointer.GiveBack();
        _pointer.ReleaseButton(MouseButton.Right);

        Assert.Equal(["Up menu/button Right", "Exit menu/button"], _scene.Sent);
    }

    [Fact]
    public void APressAndARelease_GoToWhatIsUnderThePointerThen_AsItMayHaveChangedSinceItMoved()
    {
        _pointer.MoveMouse(_onButton);
        _scene.Hit(_onButton, "menu/other");
        _scene.Sent.Clear();

        _pointer.PressButton(MouseButton.Left);
        _scene.Hit(_onButton, "menu/button/label");
        _pointer.ReleaseButton(MouseButton.Left);

        Assert.Equal(
        [
            "Down menu/other Left", "Exit menu/button", "Enter menu/other",
            "Up menu/other Left", "Exit menu/other", "Enter menu/button",
        ], _scene.Sent);
    }

    [Fact]
    public void WithNoEventSystem_ThePointerDoesntTakeTheInput()
    {
        _scene.Ready = false;

        Assert.False(_pointer.MoveMouse(_onButton));
        Assert.False(_pointer.PressButton(MouseButton.Left));
        _pointer.GiveBack();
        Assert.Empty(_scene.Sent);
    }

    [Fact]
    public void WhileTheGamesInputModuleReadsUnitysInput_ThePointerLeavesItToThat_AndDoesntTakeTheInput()
    {
        _scene.ModuleReadsUnityInput = true;

        Assert.False(_pointer.MoveMouse(_onButton));
        Assert.False(_pointer.PressButton(MouseButton.Left));
        Assert.False(_pointer.ReleaseButton(MouseButton.Left));
        _pointer.GiveBack();
        Assert.Empty(_scene.Sent);
    }

    [Fact]
    public void WhatOnlyTakesClicks_TakesThePress_AndItsClick()
    {
        _scene.Handlers["menu/button"] = [PointerEvent.Click];
        _pointer.MoveMouse(_onButton);

        _pointer.PressButton(MouseButton.Left);
        _pointer.ReleaseButton(MouseButton.Left);

        Assert.Equal(["Click menu/button Left"], _scene.Sent);
    }

    /// <summary>
    /// Vector2's operators have no body here either.
    /// </summary>
    private static Vector2 Delta(Vector2 from, Vector2 to)
    {
        return new Vector2 { x = to.x - from.x, y = to.y - from.y };
    }

    private sealed class FakeScene : IUguiScene<string>
    {
        // Vector2's own equality has no body here either
        private readonly Dictionary<(float X, float Y), string> _hits = [];

        public Dictionary<string, PointerEvent[]> Handlers { get; } = [];

        public List<string> Sent { get; } = [];

        public bool Ready { get; set; } = true;

        public bool ModuleReadsUnityInput { get; set; }

        public HashSet<string> Selectable { get; } = [];

        public string Selected { get; set; }

        public void Hit(Vector2 screenPoint, string node)
        {
            _hits[(screenPoint.x, screenPoint.y)] = node;
        }

        public string HitAt(Vector2 screenPoint)
        {
            return _hits.TryGetValue((screenPoint.x, screenPoint.y), out var hit) ? hit : null;
        }

        public string Parent(string node)
        {
            var slash = node.LastIndexOf('/');
            return slash < 0 ? null : node.Substring(0, slash);
        }

        public bool Handles(string node, PointerEvent pointerEvent)
        {
            return Handlers.TryGetValue(node, out var events) && events.Contains(pointerEvent);
        }

        public bool CanSelect(string node)
        {
            return Selectable.Contains(node);
        }

        public void Deselect()
        {
            Sent.Add($"Deselect {Selected}");
            Selected = null;
        }

        public void Send(string node, PointerEvent pointerEvent, PointerState<string> state)
        {
            Sent.Add(pointerEvent is PointerEvent.Enter or PointerEvent.Exit
                ? $"{pointerEvent} {node}"
                : $"{pointerEvent} {node} {state.Button}");
        }
    }
}
