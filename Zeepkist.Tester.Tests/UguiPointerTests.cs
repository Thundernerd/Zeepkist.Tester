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
        _scene.Handlers["menu/button"] = [PointerEvent.Enter, PointerEvent.Exit, PointerEvent.Down, PointerEvent.Up, PointerEvent.Click];
        _scene.Handlers["menu/other"] = [PointerEvent.Enter, PointerEvent.Exit, PointerEvent.Down, PointerEvent.Up, PointerEvent.Click];
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
    public void WithNoEventSystem_ThePointerDoesntTakeTheInput()
    {
        _scene.Ready = false;

        Assert.False(_pointer.MoveMouse(_onButton));
        Assert.False(_pointer.PressButton(MouseButton.Left));
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

        public void Send(string node, PointerEvent pointerEvent, PointerState<string> state)
        {
            Sent.Add(pointerEvent is PointerEvent.Enter or PointerEvent.Exit
                ? $"{pointerEvent} {node}"
                : $"{pointerEvent} {node} {state.Button}");
        }
    }
}
