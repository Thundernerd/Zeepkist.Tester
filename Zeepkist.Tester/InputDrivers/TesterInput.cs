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
    private readonly IActionDriver _actions;
    private readonly Func<float> _now;

    /// <param name="inProcess">What the in-process route sends keys and the mouse through.</param>
    /// <param name="actions">What the in-process route sends the game's input actions through.</param>
    /// <param name="now">Seconds of real time, for how long a key is held.</param>
    public TesterInput(IInputDriver inProcess, IActionDriver actions, Func<float> now)
    {
        _inProcess = inProcess;
        _actions = actions;
        _now = now;
    }

    public InputRoute Route { get; set; } = InputRoute.InProcess;

    public IEnumerator Press(Keys keys)
    {
        var driver = CurrentDriver();
        var presses = KeyNames.Parse(keys);
        RequireTaken(driver.PressKeys(presses));
        yield return null;

        // A field handles them this frame, and what reads the field can do so once this ends
        driver.ReleaseKeys(presses);
        yield return null;
    }

    public IEnumerator Type(string text)
    {
        var driver = CurrentDriver();
        foreach (var character in text)
        {
            KeyPress[] press = [KeyNames.ForCharacter(character)];
            RequireTaken(driver.PressKeys(press));
            yield return null;
            driver.ReleaseKeys(press);
        }

        yield return null;
    }

    public IEnumerator Hold(Keys keys, Seconds duration)
    {
        var driver = CurrentDriver();
        var presses = KeyNames.Parse(keys);
        var until = _now() + duration.Value;
        RequireTaken(driver.PressKeys(presses));
        yield return null;
        while (_now() < until)
            yield return null;

        driver.ReleaseKeys(presses);
        yield return null;
    }

    public IEnumerator PressAction(string action)
    {
        RequireInProcessForActions();
        RequireTaken(_actions.Set(action, 1));
        yield return null;
        _actions.Set(action, 0);
        yield return null;
    }

    public IEnumerator HoldAction(string action, Seconds duration, float value = 1)
    {
        RequireInProcessForActions();
        if (value is < -1 or > 1)
            throw new StepFailedException($"An input action's value is from -1 to 1, and this one is {value}.");

        var until = _now() + duration.Value;
        RequireTaken(_actions.Set(action, value));
        yield return null;
        while (_now() < until)
            yield return null;

        _actions.Set(action, 0);
        yield return null;
    }

    public IEnumerator MoveMouse(Vector2 screenPoint)
    {
        var driver = CurrentDriver();
        RequireTaken(driver.MoveMouse(screenPoint));

        // Imui finds the hovered control as it draws, for the frame after
        yield return null;
    }

    public IEnumerator Click(Vector2 screenPoint, MouseButton button = MouseButton.Left)
    {
        var driver = CurrentDriver();
        RequireTaken(driver.MoveMouse(screenPoint));
        yield return null;
        RequireTaken(driver.PressButton(button));
        yield return null;
        driver.ReleaseButton(button);
        yield return null;
    }

    public IEnumerator Drag(Vector2 from, IReadOnlyList<DragMove> moves, MouseButton button = MouseButton.Left)
    {
        var driver = CurrentDriver();
        var tooShort = moves.FirstOrDefault(move => move.Over < 1);
        if (tooShort != null)
            throw new StepFailedException($"A drag's move takes at least 1 frame, and this one takes {tooShort.Over}.");

        RequireTaken(driver.MoveMouse(from));
        yield return null;
        RequireTaken(driver.PressButton(button));
        yield return null;
        foreach (var delta in moves.SelectMany(FrameDeltas))
        {
            RequireTaken(driver.DragMouse(delta, button));
            yield return null;
        }

        driver.ReleaseButton(button);
        yield return null;
    }

    public void GiveBack()
    {
        _inProcess.GiveBack();
        _actions.GiveBack();
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

    /// <summary>
    /// Only for what goes down or moves. A release has already had its effect if what took the press is gone, so a key or
    /// click that closed it is no failure.
    /// </summary>
    private static void RequireTaken(bool taken)
    {
        if (!taken)
            throw new StepFailedException("Nothing in the game could take the input.");
    }

    /// <summary>
    /// Only what reads the game's input from inside it has its actions. The OS route sends keys, which reach an action
    /// only through what it's bound to.
    /// </summary>
    private void RequireInProcessForActions()
    {
        if (Route != InputRoute.InProcess)
            throw new StepFailedException("An input action can only be sent in-process, and this step is on input: os.");
    }

    private IInputDriver CurrentDriver()
    {
        return Route == InputRoute.InProcess
            ? _inProcess
            : throw new StepFailedException("input: os isn't there yet, only in-process, so this step can't send its input.");
    }
}
