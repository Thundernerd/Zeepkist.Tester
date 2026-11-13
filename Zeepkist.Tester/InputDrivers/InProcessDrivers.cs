using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// The in-process route: one driver for each of the paths the game reads input through, which all get the same input,
/// the way a player's keyboard and mouse reach all of them.
/// </summary>
internal sealed class InProcessDrivers : IInputDriver
{
    private readonly Action<Exception> _logGiveBackFailure;
    private readonly IReadOnlyList<IInputDriver> _drivers;

    /// <param name="logGiveBackFailure">Hears what a driver throws from <see cref="GiveBack"/>, which it shouldn't.</param>
    /// <param name="drivers">The drivers, which each get every call.</param>
    public InProcessDrivers(Action<Exception> logGiveBackFailure, params IInputDriver[] drivers)
    {
        _logGiveBackFailure = logGiveBackFailure;
        _drivers = drivers;
    }

    public bool PressKeys(IReadOnlyList<KeyPress> keys)
    {
        return ToAll(driver => driver.PressKeys(keys));
    }

    public bool ReleaseKeys(IReadOnlyList<KeyPress> keys)
    {
        return ToAll(driver => driver.ReleaseKeys(keys));
    }

    public bool MoveMouse(Vector2 screenPoint)
    {
        return ToAll(driver => driver.MoveMouse(screenPoint));
    }

    public bool PressButton(MouseButton button)
    {
        return ToAll(driver => driver.PressButton(button));
    }

    public bool DragMouse(Vector2 delta, MouseButton button)
    {
        return ToAll(driver => driver.DragMouse(delta, button));
    }

    public bool ReleaseButton(MouseButton button)
    {
        return ToAll(driver => driver.ReleaseButton(button));
    }

    /// <summary>
    /// Every driver lets go, whatever one before it throws, so one that breaks <see cref="IInputDriver.GiveBack"/>'s
    /// promise leaves nothing else held. What it threw is logged, not thrown, so this keeps the promise.
    /// </summary>
    public void GiveBack()
    {
        foreach (var driver in _drivers)
        {
            try
            {
                driver.GiveBack();
            }
            catch (Exception exception)
            {
                _logGiveBackFailure(exception);
            }
        }
    }

    /// <summary>
    /// A path with nothing to take it right now is no failure while another has something.
    /// </summary>
    private bool ToAll(Func<IInputDriver, bool> send)
    {
        return _drivers.Select(send).ToList().Contains(true);
    }
}
