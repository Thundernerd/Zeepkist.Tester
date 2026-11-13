using System;
using System.Collections.Generic;
using ZeepSDK.Controls;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// The game's input categories a step turned off through ZeepSDK's input overrides, as a mod's text field does, until a
/// step or the end of the scenario turns them on again.
/// </summary>
public sealed class InputOff
{
    private readonly List<IDisposable> _layers = [];

    /// <summary>Turns off the category's maps for every player.</summary>
    /// <returns>Whether ZeepSDK has an override for the category.</returns>
    public bool TurnOff(string category)
    {
        var stack = category switch
        {
            "Gameplay" => ControlsApi.GameplayInputOverride,
            "LevelEditor" => ControlsApi.LevelEditorInputOverride,
            "Menu" => ControlsApi.MenuInputOverride,
            _ => (InputOverrideStack)null,
        };
        if (stack == null)
            return false;

        _layers.Add(stack.Override(false));
        return true;
    }

    /// <summary>Turns on what <see cref="TurnOff"/> turned off.</summary>
    public void TurnOnAll()
    {
        for (var i = _layers.Count - 1; i >= 0; i--)
            _layers[i].Dispose();

        _layers.Clear();
    }
}
