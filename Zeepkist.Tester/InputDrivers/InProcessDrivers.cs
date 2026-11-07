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
    private readonly IReadOnlyList<IInputDriver> _drivers;

    public InProcessDrivers(params IInputDriver[] drivers)
    {
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

    public void GiveBack()
    {
        foreach (var driver in _drivers)
            driver.GiveBack();
    }

    /// <summary>
    /// A path with nothing to take it right now is no failure while another has something.
    /// </summary>
    private bool ToAll(Func<IInputDriver, bool> send)
    {
        return _drivers.Select(send).ToList().Contains(true);
    }
}
