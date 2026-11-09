using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.InputDrivers;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// What the game's input actions read in each frame, from the values the tester holds them at, the way the game fills them
/// from Rewired's events.
/// </summary>
public class ActionFramesTests
{
    private const int Undo = 99;
    private const int Steer = 4;

    private readonly ActionFrames _frames = new ActionFrames();

    [Fact]
    public void AButtonPressed_IsDownAndHeld_InItsFirstFrame()
    {
        _frames.Set(Undo, 1);

        Assert.Equal(["99: down held +down +held axis 1"], Next(Button));
    }

    [Fact]
    public void AButtonHeld_IsOnlyHeld_AfterItsFirstFrame()
    {
        _frames.Set(Undo, 1);
        _frames.Next();

        Assert.Equal(["99: held +held axis 1"], Next(Button));
    }

    [Fact]
    public void AButtonLetGo_IsUp_InTheFrameAfter_AndClearedInTheOneAfterThat_ThenLeftAlone()
    {
        _frames.Set(Undo, 1);
        _frames.Next();
        _frames.Set(Undo, 0);

        Assert.Equal(["99: up +up axis 0"], Next(Button));
        Assert.Equal(["99: axis 0"], Next(Button));
        Assert.Empty(Next(Button));
    }

    [Fact]
    public void AnAxisHeldLeft_ReadsItsValue_AndItsNegativeSideIsDown()
    {
        _frames.Set(Steer, -0.5f);

        Assert.Equal(["4: -down -held axis -0.5"], Next(Axis));
    }

    [Fact]
    public void AButtonForTheNegativeSide_IsDownAndHeld_WhenTheAxisGoesThatWay()
    {
        _frames.Set(Steer, -0.5f);

        Assert.Equal(["4: down held -down -held axis 1"], Next(NegativeButton));
    }

    [Fact]
    public void AButtonForTheNegativeSide_IsLeftUp_WhenTheAxisGoesTheOtherWay()
    {
        _frames.Set(Steer, 1);

        Assert.Equal(["4: +down +held axis 0"], Next(NegativeButton));
    }

    [Fact]
    public void AnAxisSwitchingSides_LetsGoOfOneAndPressesTheOther_InOneFrame()
    {
        _frames.Set(Steer, -1);
        _frames.Next();
        _frames.Set(Steer, 1);

        Assert.Equal(["4: down held -up +down +held axis 1"], Next(Axis));
    }

    [Fact]
    public void ReleaseAll_LetsGoOfEveryAction()
    {
        _frames.Set(Undo, 1);
        _frames.Set(Steer, -1);
        _frames.Next();

        _frames.ReleaseAll();

        Assert.Equal(["99: up +up axis 0", "4: -up axis 0"], Next(Axis));
    }

    private static ActionKind Axis => new ActionKind(useAsButton: false, useNegativeInput: false);

    private static ActionKind NegativeButton => new ActionKind(useAsButton: true, useNegativeInput: true);

    private static ActionKind Button => new ActionKind(useAsButton: true, useNegativeInput: false);

    private List<string> Next(ActionKind kind)
    {
        return _frames.Next().Select(frame => $"{frame.ActionId}: {Describe(frame.For(kind.UseAsButton, kind.UseNegativeInput))}").ToList();
    }

    private static string Describe(ActionFields fields)
    {
        var parts = new List<string>();
        if (fields.ButtonDown)
            parts.Add("down");
        if (fields.ButtonUp)
            parts.Add("up");
        if (fields.ButtonHeld)
            parts.Add("held");
        if (fields.NegativeButtonDown)
            parts.Add("-down");
        if (fields.NegativeButtonUp)
            parts.Add("-up");
        if (fields.NegativeButtonHeld)
            parts.Add("-held");
        if (fields.PositiveButtonDown)
            parts.Add("+down");
        if (fields.PositiveButtonUp)
            parts.Add("+up");
        if (fields.PositiveButtonHeld)
            parts.Add("+held");

        parts.Add($"axis {fields.Axis}");
        return string.Join(" ", parts);
    }

    private readonly struct ActionKind
    {
        public ActionKind(bool useAsButton, bool useNegativeInput)
        {
            UseAsButton = useAsButton;
            UseNegativeInput = useNegativeInput;
        }

        public bool UseAsButton { get; }

        public bool UseNegativeInput { get; }
    }
}
