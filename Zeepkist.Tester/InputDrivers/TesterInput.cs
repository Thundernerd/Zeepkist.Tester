using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// What each call sends in which frame, on the route it's on. One lives for the whole run, so the runner sets
/// <see cref="Route"/> as each scenario starts, and can call <see cref="GiveBack"/> as each ends.
/// </summary>
internal sealed class TesterInput : IInput
{
    private readonly IInputDriver _inProcess;
    private readonly Func<float> _now;

    /// <param name="inProcess">What the in-process route sends through.</param>
    /// <param name="now">Seconds of real time, for how long a key is held.</param>
    public TesterInput(IInputDriver inProcess, Func<float> now)
    {
        _inProcess = inProcess;
        _now = now;
    }

    public InputRoute Route { get; set; } = InputRoute.InProcess;

    public IEnumerator Press(Keys keys)
    {
        var driver = CurrentDriver();
        var presses = KeyNames.Parse(keys);
        Sent(driver.PressKeys(presses));
        yield return null;

        // A field handles them this frame, and what reads the field can do so once this ends
        Sent(driver.ReleaseKeys(presses));
        yield return null;
    }

    public IEnumerator Type(string text)
    {
        var driver = CurrentDriver();
        foreach (var character in text)
        {
            KeyPress[] press = [KeyNames.ForCharacter(character)];
            Sent(driver.PressKeys(press));
            yield return null;
            Sent(driver.ReleaseKeys(press));
        }

        yield return null;
    }

    public IEnumerator Hold(Keys keys, Seconds duration)
    {
        var driver = CurrentDriver();
        var presses = KeyNames.Parse(keys);
        var until = _now() + duration.Value;
        Sent(driver.PressKeys(presses));
        yield return null;
        while (_now() < until)
            yield return null;

        Sent(driver.ReleaseKeys(presses));
        yield return null;
    }

    public IEnumerator MoveMouse(Vector2 screenPoint)
    {
        var driver = CurrentDriver();
        Sent(driver.MoveMouse(screenPoint));

        // Imui finds the hovered control as it draws, for the frame after
        yield return null;
    }

    public IEnumerator Click(Vector2 screenPoint, MouseButton button = MouseButton.Left)
    {
        var driver = CurrentDriver();
        Sent(driver.MoveMouse(screenPoint));
        yield return null;
        Sent(driver.PressButton(button));
        yield return null;
        Sent(driver.ReleaseButton(button));
        yield return null;
    }

    public IEnumerator Drag(Vector2 from, IReadOnlyList<DragMove> moves, MouseButton button = MouseButton.Left)
    {
        var driver = CurrentDriver();
        Sent(driver.MoveMouse(from));
        yield return null;
        Sent(driver.PressButton(button));
        yield return null;
        foreach (var delta in moves.SelectMany(FrameDeltas))
        {
            Sent(driver.DragMouse(delta, button));
            yield return null;
        }

        Sent(driver.ReleaseButton(button));
        yield return null;
    }

    public void GiveBack()
    {
        _inProcess.GiveBack();
    }

    /// <summary>
    /// How far the move takes the mouse in each of its frames, in whole pixels that add up to all of it. Unity counts
    /// pixels up the screen, so a move down is below 0. Its fields are set one by one because the unit tests'
    /// UnityEngine has no method bodies, so not Vector2's constructor either.
    /// </summary>
    private static IEnumerable<Vector2> FrameDeltas(DragMove move)
    {
        for (var frame = 1; frame <= move.Over; frame++)
        {
            var pixels = (float)(Math.Round((double)move.By.Value * frame / move.Over) - Math.Round((double)move.By.Value * (frame - 1) / move.Over));
            yield return move.Sideways ? new Vector2 { x = pixels } : new Vector2 { y = -pixels };
        }
    }

    private static void Sent(bool taken)
    {
        if (!taken)
            throw new StepFailedException("Nothing in the game could take the input.");
    }

    private IInputDriver CurrentDriver()
    {
        return Route == InputRoute.InProcess
            ? _inProcess
            : throw new StepFailedException("input: os isn't there yet, only in-process, so this step can't send its input.");
    }
}
