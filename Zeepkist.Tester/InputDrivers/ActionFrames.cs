using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// The values the tester holds the game's input actions at, and what each action reads from them in a frame. An action
/// goes down in the frame its value leaves 0 and up in the frame it comes back, as Rewired reports a key. The frame after
/// that clears it, and from then on the player's own input has it to itself.
/// </summary>
internal sealed class ActionFrames
{
    // The value each action is to have, and the one it had in the frame before
    private readonly Dictionary<int, float> _values = [];
    private readonly Dictionary<int, float> _before = [];

    /// <summary>
    /// Holds the action at the value from the next frame; 0 lets go.
    /// </summary>
    public void Set(int actionId, float value)
    {
        _values[actionId] = value;
    }

    /// <summary>
    /// Lets go of every action from the next frame.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var actionId in _values.Keys.ToList())
            _values[actionId] = 0;
    }

    /// <summary>
    /// Moves on a frame, and says what each action the tester has a hand in reads in it.
    /// </summary>
    public IReadOnlyList<ActionFrame> Next()
    {
        var frames = new List<ActionFrame>();
        foreach (var actionId in _values.Keys.ToList())
        {
            var now = _values[actionId];
            _before.TryGetValue(actionId, out var before);
            frames.Add(new ActionFrame(actionId, before, now));
            if (now == 0 && before == 0)
            {
                // Cleared now, so it's the player's again
                _values.Remove(actionId);
                _before.Remove(actionId);
            }
            else
            {
                _before[actionId] = now;
            }
        }

        return frames;
    }
}
