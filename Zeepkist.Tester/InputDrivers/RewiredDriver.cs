using System.Linq;
using Rewired;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Hands the game's input actions a value by their Rewired name. The game reads each action from an
/// <see cref="InputActionScriptableObject"/>, which it fills from Rewired's events in Rewired's update. Right after that
/// update, and before any of the game's own, this writes what the action reads in that frame, so whatever the action is
/// bound to, and whoever reads it, it reads as a key or stick does. A key only reaches an action through a map the game
/// has on, and the game turns a category's maps off, as ZeepSDK's input overrides do while a text field has focus. So an
/// action none of whose maps are on isn't sent: a step fails for it, and one held when it goes off lets go.
/// </summary>
internal sealed class RewiredDriver : IActionDriver
{
    private const int AnyAction = -1;

    // Static for the patch on Rewired's update to reach, so every instance shares it
    private static readonly ActionFrames _frames = new ActionFrames();

    public bool Set(string action, float value)
    {
        if (!ReInput.isReady)
            return false;

        var actionId = ReInput.mapping.GetActionId(action);
        if (actionId < 0)
            throw new StepFailedException($"The game has no input action called {action}.");

        if (value != 0 && !ReInput.players.AllPlayers.Any(player => IsOn(player, actionId)))
            throw new StepFailedException($"The game has {action} turned off right now.");

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

        // Every player's, since each has its own for one action; the ones without a controller go unread. One with id -1
        // takes any button, as the game's own handler gives it every button's event.
        var actions = Resources.FindObjectsOfTypeAll<InputActionScriptableObject>();
        foreach (var frame in frames)
        {
            var on = false;
            foreach (var action in actions)
            {
                // One whose player has the action off gets nothing: the game cleared its fields as it turned it off
                if ((action.ID == frame.ActionId || action.ID == AnyAction) && IsOn(action.Player?.player, frame.ActionId))
                {
                    Write(action, frame.For(action.UseAsButton, action.UseNegativeInput));
                    on = true;
                }
            }

            // Turned off for every player since the step set it, so it lets go, as a held key's action does
            if (!on)
                _frames.Set(frame.ActionId, 0);
        }
    }

    /// <summary>
    /// Whether a key could reach the action for the player: one of its controllers has a map the game has on with
    /// something bound to it.
    /// </summary>
    private static bool IsOn(Player player, int actionId)
    {
        return player != null && player.controllers.maps.ElementMapsWithAction(actionId, true).Any();
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
