using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// What each call hands the drivers in each frame, and which route takes it.
/// </summary>
public class TesterInputTests
{
    // The unit tests' UnityEngine has no method bodies, so not Vector2's constructor either
    private static readonly Vector2 _point = new Vector2 { x = 10, y = 20 };

    private readonly FakeDriver _driver = new FakeDriver();
    private readonly TesterInput _input;
    private float _now;

    public TesterInputTests()
    {
        _input = new TesterInput(_driver, () => _now);
    }

    private static Func<IInput, IEnumerator>[] Calls =>
    [
        input => input.Press("a"),
        input => input.Type("a"),
        input => input.Hold("a", 1),
        input => input.MoveMouse(_point),
        input => input.Click(_point),
        input => input.Drag(_point, [new DragMove { By = 1 }]),
    ];

    [Fact]
    public void Press_SendsItsKeysInOneFrame_AndLetsGoInTheNext()
    {
        Assert.Equal(
        [
            ["down A", "down B"],
            ["up A", "up B"],
        ], Frames(_input.Press("a b")));
    }

    [Fact]
    public void Press_SendsEveryEscapeOfOnePressInOneFrame_AsAQuickDoublePressDoes()
    {
        Assert.Equal(
        [
            ["down Escape", "down Escape"],
            ["up Escape", "up Escape"],
        ], Frames(_input.Press("escape escape")));
    }

    [Fact]
    public void Type_SendsOneKeyAFrame_LettingGoOfTheOneBefore()
    {
        Assert.Equal(
        [
            ["down H"],
            ["up H", "down I"],
            ["up I", "down None ("],
            ["up None"],
        ], Frames(_input.Type("Hi(")));
    }

    [Fact]
    public void Hold_HoldsTheKeys_UntilTheTimeIsUp()
    {
        var routine = _input.Hold("a b", 2);

        Assert.Equal(["down A", "down B"], NextFrame(routine));
        Assert.Empty(NextFrame(routine));
        _now = 1.9f;
        Assert.Empty(NextFrame(routine));
        _now = 2;
        Assert.Equal(["up A", "up B"], NextFrame(routine));
        Assert.False(routine.MoveNext());
    }

    [Fact]
    public void MoveMouse_GivesTheFrameItNeedsToFindWhatsThere()
    {
        Assert.Equal([["move 10,20"]], Frames(_input.MoveMouse(_point)));
    }

    [Fact]
    public void Click_MovesThenPressesThenLetsGo_EachInAFrameOfItsOwn()
    {
        Assert.Equal(
        [
            ["move 10,20"],
            ["press Right"],
            ["release Right"],
        ], Frames(_input.Click(_point, MouseButton.Right)));
    }

    [Fact]
    public void Drag_MakesEachMoveInTurn_InWholePixelsThatAddUpToIt()
    {
        var moves = new[] { new DragMove { By = 10, Over = 3 }, new DragMove { By = -4 } };

        Assert.Equal(
        [
            ["move 10,20"],
            ["press Left"],
            ["drag 0,-3 Left"],
            ["drag 0,-4 Left"],
            ["drag 0,-3 Left"],
            ["drag 0,4 Left"],
            ["release Left"],
        ], Frames(_input.Drag(_point, moves)));
    }

    [Fact]
    public void Drag_GoesRightOrLeft_ForASidewaysMove()
    {
        var moves = new[] { new DragMove { By = 5, Sideways = true }, new DragMove { By = -2, Sideways = true } };

        var drags = Frames(_input.Drag(_point, moves)).SelectMany(frame => frame).Where(item => item.StartsWith("drag"));

        Assert.Equal(["drag 5,0 Left", "drag -2,0 Left"], drags);
    }

    [Fact]
    public void ARouteStartsInProcess()
    {
        Assert.Equal(InputRoute.InProcess, _input.Route);
    }

    [Fact]
    public void TheOsRoute_FailsTheStepThatSendsInput_WithoutReachingADriver()
    {
        _input.Route = InputRoute.Os;

        foreach (var call in Calls)
        {
            var routine = call(_input);
            var failure = Assert.Throws<StepFailedException>(() => routine.MoveNext());
            Assert.Equal("input: os isn't there yet, only in-process, so this step can't send its input.", failure.Message);
        }

        Assert.Empty(_driver.Events);
    }

    [Fact]
    public void AStepCanSwitchTheRouteBack()
    {
        _input.Route = InputRoute.Os;
        _input.Route = InputRoute.InProcess;

        Assert.Equal([["move 10,20"]], Frames(_input.MoveMouse(_point)));
    }

    [Fact]
    public void WhenNothingCanTakeTheInput_TheStepFails()
    {
        _driver.Takes = false;

        foreach (var call in Calls)
        {
            var routine = call(_input);
            var failure = Assert.Throws<StepFailedException>(() => routine.MoveNext());
            Assert.Equal("Nothing in the game could take the input.", failure.Message);
        }
    }

    [Theory]
    [InlineData(InputRoute.InProcess)]
    [InlineData(InputRoute.Os)]
    public void GiveBack_ReachesTheDrivers_WhicheverTheRoute(InputRoute route)
    {
        _input.Route = route;

        _input.GiveBack();

        Assert.Equal(["give back"], _driver.Events);
    }

    [Fact]
    public void GiveBack_LeavesTheRouteAlone()
    {
        _input.Route = InputRoute.Os;

        _input.GiveBack();

        Assert.Equal(InputRoute.Os, _input.Route);
    }

    [Fact]
    public void TheInProcessRoute_HandsEveryDriverEachCall()
    {
        var other = new FakeDriver();
        var composite = new InProcessDrivers(_driver, other);

        Assert.True(composite.PressButton(MouseButton.Left));
        composite.GiveBack();

        Assert.Equal(["press Left", "give back"], _driver.Events);
        Assert.Equal(["press Left", "give back"], other.Events);
    }

    [Fact]
    public void TheInProcessRoute_TakesTheInput_WhenAnyDriverDoes()
    {
        var composite = new InProcessDrivers(new FakeDriver { Takes = false }, _driver);

        Assert.True(composite.ReleaseButton(MouseButton.Left));
        Assert.Equal(["release Left"], _driver.Events);
    }

    [Fact]
    public void TheInProcessRoute_DoesntTakeTheInput_WhenNoDriverDoes()
    {
        var composite = new InProcessDrivers(new FakeDriver { Takes = false }, new FakeDriver { Takes = false });

        Assert.False(composite.MoveMouse(_point));
    }

    private List<string> NextFrame(IEnumerator routine)
    {
        _driver.Events.Clear();
        Assert.True(routine.MoveNext());
        return [.. _driver.Events];
    }

    private List<List<string>> Frames(IEnumerator routine)
    {
        var frames = new List<List<string>>();
        _driver.Events.Clear();
        while (routine.MoveNext())
        {
            frames.Add([.. _driver.Events]);
            _driver.Events.Clear();
        }

        return frames;
    }

    private sealed class FakeDriver : IInputDriver
    {
        public List<string> Events { get; } = [];

        public bool Takes { get; set; } = true;

        public bool PressKeys(IReadOnlyList<KeyPress> keys)
        {
            return Add(keys.Select(key => $"down {key.Key}{(key.Key == KeyCode.None ? $" {key.Character}" : string.Empty)}"));
        }

        public bool ReleaseKeys(IReadOnlyList<KeyPress> keys)
        {
            return Add(keys.Select(key => $"up {key.Key}"));
        }

        public bool MoveMouse(Vector2 screenPoint)
        {
            return Add([$"move {screenPoint.x},{screenPoint.y}"]);
        }

        public bool PressButton(MouseButton button)
        {
            return Add([$"press {button}"]);
        }

        public bool DragMouse(Vector2 delta, MouseButton button)
        {
            return Add([$"drag {delta.x},{delta.y} {button}"]);
        }

        public bool ReleaseButton(MouseButton button)
        {
            return Add([$"release {button}"]);
        }

        public void GiveBack()
        {
            Events.Add("give back");
        }

        private bool Add(IEnumerable<string> events)
        {
            if (Takes)
                Events.AddRange(events);

            return Takes;
        }
    }
}
