using Rewired;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Hands the game's input actions a value by their Rewired name. The game reads each action from an
/// <see cref="InputActionScriptableObject"/>, which it fills from Rewired's events in Rewired's update. Right after that
/// update, and before any of the game's own, this writes what the action reads in that frame, so whatever the action is
/// bound to, and whoever reads it, it reads as a key or stick does.
/// </summary>
internal sealed class RewiredDriver : IActionDriver
{
    private static readonly ActionFrames _frames = new ActionFrames();

    public bool Set(string action, float value)
    {
        if (!ReInput.isReady)
            return false;

        var actionId = ReInput.mapping.GetActionId(action);
        if (actionId < 0)
            throw new StepFailedException($"The game has no input action called {action}.");

        _frames.Set(actionId, value);
        return true;
    }

    public void GiveBack()
    {
        _frames.ReleaseAll();
    }

    /// <summary>
    /// Writes this frame's values into the game's actions, once Rewired has written the player's.
    /// </summary>
    public static void AfterRewiredUpdate()
    {
        var frames = _frames.Next();
        if (frames.Count == 0)
            return;

        // Every player's, since each has its own for one action; the ones without a controller go unread
        var actions = Resources.FindObjectsOfTypeAll<InputActionScriptableObject>();
        foreach (var frame in frames)
        {
            foreach (var action in actions)
            {
                if (action.ID == frame.ActionId)
                    Write(action, frame.For(action.UseAsButton, action.UseNegativeInput));
            }
        }
    }

    private static void Write(InputActionScriptableObject action, ActionFields fields)
    {
        action.buttonDown = fields.ButtonDown;
        action.buttonUp = fields.ButtonUp;
        action.buttonHeld = fields.ButtonHeld;
        action.negativeButtonDown = fields.NegativeButtonDown;
        action.negativeButtonUp = fields.NegativeButtonUp;
        action.negativeButtonHeld = fields.NegativeButtonHeld;
        action.positiveButtonDown = fields.PositiveButtonDown;
        action.positiveButtonUp = fields.PositiveButtonUp;
        action.positiveButtonHeld = fields.PositiveButtonHeld;
        action.axis = fields.Axis;
    }
}
