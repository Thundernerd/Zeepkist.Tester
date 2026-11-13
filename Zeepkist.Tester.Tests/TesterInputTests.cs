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
    private readonly FakeActions _actions = new FakeActions();
    private readonly List<Exception> _logged = [];
    private readonly TesterInput _input;
    private float _now;

    public TesterInputTests()
    {
        _input = new TesterInput(_driver, _actions, () => _now, _logged.Add);
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

    private static Func<IInput, IEnumerator>[] ActionCalls =>
    [
        input => input.PressAction("EditorUndo"),
        input => input.HoldAction("Horizontal", 1, -1),
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
    public void PressAction_HoldsTheActionDownForOneFrame_AndLetsGoInTheNext()
    {
        Assert.Equal(
        [
            ["EditorUndo 1"],
            ["EditorUndo 0"],
        ], Frames(_input.PressAction("EditorUndo"), _actions.Events));
    }

    [Fact]
    public void HoldAction_HoldsTheActionAtItsValue_UntilTheTimeIsUp()
    {
        var routine = _input.HoldAction("Horizontal", 2, -0.5f);

        Assert.Equal(["Horizontal -0.5"], NextFrame(routine, _actions.Events));
        Assert.Empty(NextFrame(routine, _actions.Events));
        _now = 1.9f;
        Assert.Empty(NextFrame(routine, _actions.Events));
        _now = 2;
        Assert.Equal(["Horizontal 0"], NextFrame(routine, _actions.Events));
        Assert.False(routine.MoveNext());
    }

    [Theory]
    [InlineData(1.5f)]
    [InlineData(-2f)]
    public void HoldAction_FailsTheStep_ForAValuePastFull_BeforeSendingAnything(float value)
    {
        var routine = _input.HoldAction("Horizontal", 1, value);

        var failure = Assert.Throws<StepFailedException>(() => routine.MoveNext());

        Assert.Equal($"An input action's value is from -1 to 1, and this one is {value}.", failure.Message);
        Assert.Empty(_actions.Events);
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
    public void TheOsRoute_FailsTheStepThatSendsAnAction_WithoutReachingTheGame()
    {
        _input.Route = InputRoute.Os;

        foreach (var call in ActionCalls)
        {
            var routine = call(_input);
            var failure = Assert.Throws<StepFailedException>(() => routine.MoveNext());
            Assert.Equal("An input action can only be sent in-process, and this step is on input: os.", failure.Message);
        }

        Assert.Empty(_actions.Events);
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

    [Fact]
    public void WhenNothingCanTakeTheAction_TheStepFails()
    {
        _actions.Takes = false;

        foreach (var call in ActionCalls)
        {
            var routine = call(_input);
            var failure = Assert.Throws<StepFailedException>(() => routine.MoveNext());
            Assert.Equal("Nothing in the game could take the input.", failure.Message);
        }
    }

    [Fact]
    public void WhenNothingCanTakeTheRelease_TheInputWasStillSent_SoTheStepGoesOn()
    {
        var routine = _input.Click(_point);
        NextFrame(routine);
        NextFrame(routine);
        _driver.Takes = false;

        Assert.True(routine.MoveNext());
        Assert.False(routine.MoveNext());
    }

    [Fact]
    public void Drag_FailsTheStep_ForAMoveOfNoFrames_BeforeSendingAnything()
    {
        var routine = _input.Drag(_point, [new DragMove { By = 5, Over = 0 }]);

        var failure = Assert.Throws<StepFailedException>(() => routine.MoveNext());

        Assert.Equal("A drag's move takes at least 1 frame, and this one takes 0.", failure.Message);
        Assert.Empty(_driver.Events);
    }

    [Theory]
    [InlineData(InputRoute.InProcess)]
    [InlineData(InputRoute.Os)]
    public void GiveBack_ReachesTheDrivers_WhicheverTheRoute(InputRoute route)
    {
        _input.Route = route;

        _input.GiveBack();

        Assert.Equal(["give back"], _driver.Events);
        Assert.Equal(["give back"], _actions.Events);
    }

    [Fact]
    public void GiveBack_LetsGoOfTheActions_WhenTheKeysAndMouseThrow_AndLogsWhatEachThrew()
    {
        var keysThrew = new InvalidOperationException("keys stuck");
        var actionsThrew = new InvalidOperationException("actions stuck");
        _driver.GiveBackThrows = keysThrew;
        _actions.GiveBackThrows = actionsThrew;

        _input.GiveBack();

        Assert.Equal([keysThrew, actionsThrew], _logged);
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
        var composite = new InProcessDrivers(_ => { }, _driver, other);

        Assert.True(composite.PressButton(MouseButton.Left));
        composite.GiveBack();

        Assert.Equal(["press Left", "give back"], _driver.Events);
        Assert.Equal(["press Left", "give back"], other.Events);
    }

    [Fact]
    public void TheInProcessRoute_GivesBackOnEveryDriver_WhenOneThrows_AndLogsWhatItThrew()
    {
        var thrown = new InvalidOperationException("stuck");
        var logged = new List<Exception>();
        var composite = new InProcessDrivers(logged.Add, new FakeDriver { GiveBackThrows = thrown }, _driver);

        composite.GiveBack();

        Assert.Equal(["give back"], _driver.Events);
        Assert.Equal([thrown], logged);
    }

    [Fact]
    public void TheInProcessRoute_TakesTheInput_WhenAnyDriverDoes()
    {
        var composite = new InProcessDrivers(_ => { }, new FakeDriver { Takes = false }, _driver);

        Assert.True(composite.ReleaseButton(MouseButton.Left));
        Assert.Equal(["release Left"], _driver.Events);
    }

    [Fact]
    public void TheInProcessRoute_DoesntTakeTheInput_WhenNoDriverDoes()
    {
        var composite = new InProcessDrivers(_ => { }, new FakeDriver { Takes = false }, new FakeDriver { Takes = false });

        Assert.False(composite.MoveMouse(_point));
    }

    private static List<string> NextFrame(IEnumerator routine, List<string> events)
    {
        events.Clear();
        Assert.True(routine.MoveNext());
        return [.. events];
    }

    private static List<List<string>> Frames(IEnumerator routine, List<string> events)
    {
        var frames = new List<List<string>>();
        events.Clear();
        while (routine.MoveNext())
        {
            frames.Add([.. events]);
            events.Clear();
        }

        return frames;
    }

    private List<string> NextFrame(IEnumerator routine)
    {
        return NextFrame(routine, _driver.Events);
    }

    private List<List<string>> Frames(IEnumerator routine)
    {
        return Frames(routine, _driver.Events);
    }

    private sealed class FakeActions : IActionDriver
    {
        public List<string> Events { get; } = [];

        public bool Takes { get; set; } = true;

        public bool Set(string action, float value)
        {
            if (Takes)
                Events.Add($"{action} {value}");

            return Takes;
        }

        public Exception GiveBackThrows { get; set; }

        public void GiveBack()
        {
            if (GiveBackThrows != null)
                throw GiveBackThrows;

            Events.Add("give back");
        }
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

        public Exception GiveBackThrows { get; set; }

        public void GiveBack()
        {
            if (GiveBackThrows != null)
                throw GiveBackThrows;

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
