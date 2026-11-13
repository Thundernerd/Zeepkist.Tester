using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// What Unity's own <c>Input</c> reports in each frame for what the driver was handed. Input handed over in a frame is
/// seen from the next, as the game's <c>Update</c> has already read that one's.
/// </summary>
public class LegacyInputDriverTests
{
    private readonly LegacyInputDriver _driver;
    private int _frame = 10;
    private float _deltaTime = 0.1f;

    public LegacyInputDriverTests()
    {
        _driver = new LegacyInputDriver(() => _frame, () => _deltaTime);
    }

    [Fact]
    public void PressKeys_IsDownForOneFrame_AndHeldUntilReleased()
    {
        Assert.True(_driver.PressKeys([Key(KeyCode.A)]));
        Assert.False(_driver.GetKey(KeyCode.A));
        Assert.False(_driver.GetKeyDown(KeyCode.A));

        _frame = 11;
        Assert.True(_driver.GetKey(KeyCode.A));
        Assert.True(_driver.GetKeyDown(KeyCode.A));
        Assert.False(_driver.GetKeyUp(KeyCode.A));

        _frame = 12;
        Assert.True(_driver.GetKey(KeyCode.A));
        Assert.False(_driver.GetKeyDown(KeyCode.A));
    }

    [Fact]
    public void ReleaseKeys_IsUpForOneFrame_AndNoLongerHeld()
    {
        _driver.PressKeys([Key(KeyCode.A)]);
        _frame = 11;
        Assert.True(_driver.ReleaseKeys([Key(KeyCode.A)]));
        Assert.True(_driver.GetKey(KeyCode.A));

        _frame = 12;
        Assert.False(_driver.GetKey(KeyCode.A));
        Assert.True(_driver.GetKeyUp(KeyCode.A));
        Assert.False(_driver.GetKeyDown(KeyCode.A));

        _frame = 13;
        Assert.False(_driver.GetKeyUp(KeyCode.A));
    }

    [Fact]
    public void ReleaseKeys_InTheFrameOfThePress_IsDownAndUpInOneFrame_AsAQuickTapIs()
    {
        _driver.PressKeys([Key(KeyCode.A)]);
        _driver.ReleaseKeys([Key(KeyCode.A)]);

        _frame = 11;
        Assert.True(_driver.GetKeyDown(KeyCode.A));
        Assert.True(_driver.GetKeyUp(KeyCode.A));
        Assert.False(_driver.GetKey(KeyCode.A));
    }

    [Fact]
    public void PressKeys_InTheFrameOfItsRelease_IsUpInOneFrame_AndDownInTheNext_AsTypingADoubledLetterIs()
    {
        _driver.PressKeys([Key(KeyCode.A)]);
        _frame = 11;
        _driver.ReleaseKeys([Key(KeyCode.A)]);
        _driver.PressKeys([Key(KeyCode.A)]);
        Assert.True(_driver.GetKey(KeyCode.A));
        Assert.True(_driver.GetKeyDown(KeyCode.A));

        _frame = 12;
        Assert.True(_driver.GetKeyUp(KeyCode.A));
        Assert.False(_driver.GetKeyDown(KeyCode.A));
        Assert.False(_driver.GetKey(KeyCode.A));

        _frame = 13;
        Assert.True(_driver.GetKeyDown(KeyCode.A));
        Assert.False(_driver.GetKeyUp(KeyCode.A));
        Assert.True(_driver.GetKey(KeyCode.A));
    }

    [Fact]
    public void PressKeys_AfterAQuickTapInTheSameFrame_IsDownAgainInTheFrameAfterTheTap()
    {
        _driver.PressKeys([Key(KeyCode.A)]);
        _driver.ReleaseKeys([Key(KeyCode.A)]);
        _driver.PressKeys([Key(KeyCode.A)]);

        _frame = 11;
        Assert.True(_driver.GetKeyDown(KeyCode.A));
        Assert.True(_driver.GetKeyUp(KeyCode.A));
        Assert.False(_driver.GetKey(KeyCode.A));

        _frame = 12;
        Assert.True(_driver.GetKeyDown(KeyCode.A));
        Assert.True(_driver.GetKey(KeyCode.A));
    }

    [Fact]
    public void PressKeys_OfAKeyAlreadyHeld_DoesntPutItDownAgain()
    {
        _driver.PressKeys([Key(KeyCode.Escape)]);
        _frame = 12;
        _driver.PressKeys([Key(KeyCode.Escape)]);

        _frame = 13;
        Assert.False(_driver.GetKeyDown(KeyCode.Escape));
        Assert.True(_driver.GetKey(KeyCode.Escape));
    }

    [Fact]
    public void ReleaseKeys_OfAKeyPressedTwice_KeepsItHeldUntilBothAreReleased()
    {
        _driver.PressKeys([Key(KeyCode.Escape), Key(KeyCode.Escape)]);
        _frame = 11;
        _driver.ReleaseKeys([Key(KeyCode.Escape)]);

        _frame = 12;
        Assert.True(_driver.GetKey(KeyCode.Escape));
        _driver.ReleaseKeys([Key(KeyCode.Escape)]);

        _frame = 13;
        Assert.False(_driver.GetKey(KeyCode.Escape));
        Assert.True(_driver.GetKeyUp(KeyCode.Escape));
    }

    [Fact]
    public void PressKeys_HoldsEachModifier_AsTheLeftOneOfItsKeys()
    {
        _driver.PressKeys([Key(KeyCode.Z, EventModifiers.Control | EventModifiers.Shift | EventModifiers.Alt | EventModifiers.Command)]);

        _frame = 11;
        Assert.True(_driver.GetKeyDown(KeyCode.Z));
        Assert.True(_driver.GetKey(KeyCode.LeftControl));
        Assert.True(_driver.GetKey(KeyCode.LeftShift));
        Assert.True(_driver.GetKey(KeyCode.LeftAlt));
        Assert.True(_driver.GetKey(KeyCode.LeftCommand));

        _driver.ReleaseKeys([Key(KeyCode.Z, EventModifiers.Control | EventModifiers.Shift | EventModifiers.Alt | EventModifiers.Command)]);
        _frame = 12;
        Assert.False(_driver.GetKey(KeyCode.LeftControl));
        Assert.True(_driver.GetKeyUp(KeyCode.LeftShift));
    }

    [Fact]
    public void PressKeys_OfOnlyACharacterWithoutAKey_IsNotTaken()
    {
        Assert.False(_driver.PressKeys([new KeyPress(KeyCode.None, '(', EventModifiers.None)]));

        _frame = 11;
        Assert.False(_driver.GetKey(KeyCode.None));
        Assert.False(_driver.AnyKey);
    }

    [Fact]
    public void MousePosition_IsThePlayersUntilTheMouseIsMoved()
    {
        Assert.Null(_driver.MousePosition);

        Assert.True(_driver.MoveMouse(Point(100, 200)));

        Assert.Equal((100f, 200f, 0f), Coordinates(_driver.MousePosition));
    }

    [Fact]
    public void PressButton_IsDownForOneFrame_HeldUntilReleased_AndUpForOneFrame()
    {
        Assert.True(_driver.PressButton(MouseButton.Right));

        _frame = 11;
        Assert.True(_driver.GetMouseButtonDown(1));
        Assert.True(_driver.GetMouseButton(1));
        Assert.True(_driver.GetKey(KeyCode.Mouse1));
        Assert.False(_driver.GetMouseButton(0));

        _frame = 12;
        Assert.False(_driver.GetMouseButtonDown(1));
        Assert.True(_driver.ReleaseButton(MouseButton.Right));
        Assert.True(_driver.GetMouseButton(1));

        _frame = 13;
        Assert.False(_driver.GetMouseButton(1));
        Assert.True(_driver.GetMouseButtonUp(1));
    }

    [Fact]
    public void DragMouse_MovesTheMouseByTheDelta()
    {
        _driver.MoveMouse(Point(100, 200));
        _driver.PressButton(MouseButton.Left);

        Assert.True(_driver.DragMouse(Point(5, -20), MouseButton.Left));

        Assert.Equal((105f, 180f, 0f), Coordinates(_driver.MousePosition));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnyKey_IsAKeyOrAMouseButtonHeld_AndAnyKeyDown_OneThatWentDown(bool mouse)
    {
        if (mouse)
            _driver.PressButton(MouseButton.Left);
        else
            _driver.PressKeys([Key(KeyCode.A)]);

        Assert.False(_driver.AnyKey);
        _frame = 11;
        Assert.True(_driver.AnyKey);
        Assert.True(_driver.AnyKeyDown);

        _frame = 12;
        Assert.True(_driver.AnyKey);
        Assert.False(_driver.AnyKeyDown);
        if (mouse)
            _driver.ReleaseButton(MouseButton.Left);
        else
            _driver.ReleaseKeys([Key(KeyCode.A)]);

        _frame = 13;
        Assert.False(_driver.AnyKey);
    }

    [Fact]
    public void GiveBack_LetsGoOfWhatsHeld_AndHandsBackThePlayersMouse()
    {
        _driver.PressKeys([Key(KeyCode.A, EventModifiers.Control), Key(KeyCode.A)]);
        _driver.MoveMouse(Point(100, 200));
        _driver.PressButton(MouseButton.Left);
        _frame = 11;

        _driver.GiveBack();

        _frame = 12;
        Assert.False(_driver.AnyKey);
        Assert.True(_driver.GetKeyUp(KeyCode.A));
        Assert.True(_driver.GetKeyUp(KeyCode.LeftControl));
        Assert.True(_driver.GetMouseButtonUp(0));
        Assert.Null(_driver.MousePosition);
    }

    [Fact]
    public void GiveBack_InTheFrameOfAPressOrDrag_DropsWhatTheGameHasntSeen()
    {
        _driver.MoveMouse(Point(100, 200));
        _driver.PressButton(MouseButton.Left);
        _driver.PressKeys([Key(KeyCode.A)]);
        _driver.DragMouse(Point(5, 0), MouseButton.Left);

        _driver.GiveBack();

        _frame = 11;
        Assert.False(_driver.GetMouseButtonDown(0));
        Assert.False(_driver.GetMouseButtonUp(0));
        Assert.False(_driver.GetKeyDown(KeyCode.A));
        Assert.False(_driver.AnyKeyDown);
        Assert.Equal(0, _driver.Axis("mouse x", 0));
    }

    [Fact]
    public void GiveBack_WithNothingHeld_DoesNothing()
    {
        _driver.GiveBack();

        _frame = 11;
        Assert.False(_driver.AnyKey);
        Assert.False(_driver.AnyKeyDown);
        Assert.Null(_driver.MousePosition);
    }

    [Theory]
    [InlineData((int)KeyCode.A, "a")]
    [InlineData((int)KeyCode.Alpha1, "1")]
    [InlineData((int)KeyCode.Space, "space")]
    [InlineData((int)KeyCode.Return, "return")]
    [InlineData((int)KeyCode.Escape, "escape")]
    [InlineData((int)KeyCode.PageUp, "page up")]
    [InlineData((int)KeyCode.UpArrow, "up")]
    [InlineData((int)KeyCode.Slash, "/")]
    [InlineData((int)KeyCode.LeftBracket, "[")]
    [InlineData((int)KeyCode.LeftShift, "left shift")]
    [InlineData((int)KeyCode.LeftControl, "Left Ctrl")]
    [InlineData((int)KeyCode.LeftAlt, "left alt")]
    [InlineData((int)KeyCode.LeftCommand, "left cmd")]
    [InlineData((int)KeyCode.Mouse0, "mouse 0")]
    // xUnit doesn't find a theory whose data is one of Unity's enums
    public void GetKey_ByName_FindsTheKeyByUnitysNameForIt(int key, string name)
    {
        _driver.PressKeys([Key((KeyCode)key)]);

        _frame = 11;
        Assert.True(_driver.GetKey(name));
        Assert.True(_driver.GetKeyDown(name));
        Assert.False(_driver.GetKeyUp(name));
        Assert.False(_driver.GetKey("b"));
    }

    [Theory]
    [InlineData((int)KeyCode.RightArrow, "Horizontal", 1)]
    [InlineData((int)KeyCode.D, "Horizontal", 1)]
    [InlineData((int)KeyCode.LeftArrow, "Horizontal", -1)]
    [InlineData((int)KeyCode.A, "Horizontal", -1)]
    [InlineData((int)KeyCode.UpArrow, "Vertical", 1)]
    [InlineData((int)KeyCode.W, "Vertical", 1)]
    [InlineData((int)KeyCode.DownArrow, "Vertical", -1)]
    [InlineData((int)KeyCode.S, "Vertical", -1)]
    [InlineData((int)KeyCode.D, "Vertical", 0)]
    public void AxisRaw_OfTheGamesKeyAxes_IsWhereItsHeldKeysPush(int key, string axis, float expected)
    {
        Assert.Equal(0, _driver.Axis(axis, 0));
        _driver.PressKeys([Key((KeyCode)key)]);

        _frame = 11;
        Assert.Equal(expected, _driver.AxisRaw(axis, 0));
    }

    [Fact]
    public void Axis_OfAKeyAxis_RampsWithTheGamesSensitivity_WhileAxisRawSnaps()
    {
        _driver.PressKeys([Key(KeyCode.D)]);

        _frame = 11;
        Assert.Equal(0.3f, _driver.Axis("Horizontal", 0), 3);
        Assert.Equal(1, _driver.AxisRaw("Horizontal", 0));

        _frame = 12;
        Assert.Equal(0.6f, _driver.Axis("Horizontal", 0), 3);

        _frame = 13;
        Assert.Equal(0.9f, _driver.Axis("Horizontal", 0), 3);

        _frame = 14;
        Assert.Equal(1, _driver.Axis("Horizontal", 0), 3);
        Assert.Equal(1, _driver.AxisRaw("Horizontal", 0));
    }

    [Fact]
    public void Axis_OfAKeyAxis_IsTheSameWhenReadTwiceInAFrame()
    {
        _driver.PressKeys([Key(KeyCode.D)]);

        _frame = 11;
        _driver.Axis("Horizontal", 0);
        Assert.Equal(0.3f, _driver.Axis("Horizontal", 0), 3);
    }

    [Fact]
    public void Axis_OfAKeyAxis_FallsBackWithTheGamesGravity_OnceLetGo()
    {
        _driver.PressKeys([Key(KeyCode.D)]);
        _frame = 11;
        _driver.ReleaseKeys([Key(KeyCode.D)]);
        Assert.Equal(0.3f, _driver.Axis("Horizontal", 0), 3);

        _frame = 12;
        Assert.Equal(0, _driver.Axis("Horizontal", 0), 3);
        Assert.Equal(0, _driver.AxisRaw("Horizontal", 0));
    }

    [Fact]
    public void Axis_OfAKeyAxis_SnapsToZero_WhenTheOtherWayIsPressed()
    {
        _driver.PressKeys([Key(KeyCode.D)]);
        _frame = 14;
        Assert.Equal(1, _driver.Axis("Horizontal", 0), 3);
        _driver.ReleaseKeys([Key(KeyCode.D)]);
        _driver.PressKeys([Key(KeyCode.A)]);

        _frame = 15;
        Assert.Equal(-0.3f, _driver.Axis("Horizontal", 0), 3);
    }

    [Fact]
    public void Axis_OfAKeyAxis_WithBothWaysHeld_IsZero()
    {
        _driver.PressKeys([Key(KeyCode.A), Key(KeyCode.D)]);

        _frame = 11;
        Assert.Equal(0, _driver.Axis("Horizontal", 0));
    }

    [Fact]
    public void Axis_OfTheMouse_IsWhatTheDragMovedItInTheFrameBefore_TimesTheAxissSensitivity()
    {
        _driver.MoveMouse(Point(100, 200));
        _driver.PressButton(MouseButton.Left);
        _frame = 11;
        _driver.DragMouse(Point(5, -20), MouseButton.Left);
        _driver.DragMouse(Point(5, 0), MouseButton.Left);
        Assert.Equal(0, _driver.Axis("mouse x", 0));

        _frame = 12;
        Assert.Equal(10, _driver.Axis("mouse x", 0));
        Assert.Equal(-20, _driver.Axis("mouse y", 0));
        Assert.Equal(1, _driver.Axis("Mouse X", 0), 3);
        Assert.Equal(-2, _driver.Axis("Mouse Y", 0), 3);

        _frame = 13;
        Assert.Equal(0, _driver.Axis("mouse x", 0));
    }

    [Fact]
    public void Axis_AddsToThePlayers_KeepingAKeyAxisWithinOne()
    {
        _driver.PressKeys([Key(KeyCode.D)]);
        _driver.MoveMouse(Point(0, 0));
        _driver.DragMouse(Point(5, 0), MouseButton.Left);

        _frame = 11;
        Assert.Equal(1, _driver.AxisRaw("Horizontal", 1));
        Assert.Equal(0.5f, _driver.AxisRaw("Horizontal", -0.5f));
        Assert.Equal(8, _driver.Axis("mouse x", 3));
        Assert.Equal(0.25f, _driver.Axis("Fire1", 0.25f));
    }

    [Fact]
    public void Axis_ItDoesntKnow_IsZero()
    {
        _driver.PressKeys([Key(KeyCode.D)]);

        _frame = 11;
        Assert.Equal(0, _driver.Axis("Fire1", 0));
    }

    private static (float X, float Y, float Z)? Coordinates(Vector3? position)
    {
        return position is { } value ? (value.x, value.y, value.z) : null;
    }

    // The unit tests' UnityEngine has no method bodies, so not Vector2's constructor either
    private static Vector2 Point(float x, float y)
    {
        return new Vector2 { x = x, y = y };
    }

    private static KeyPress Key(KeyCode key, EventModifiers modifiers = EventModifiers.None)
    {
        return new KeyPress(key, '\0', modifiers);
    }
}
