namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// One action's value in the frame before and in this one.
/// </summary>
internal readonly struct ActionFrame
{
    public ActionFrame(int actionId, float before, float now)
    {
        ActionId = actionId;
        Before = before;
        Now = now;
    }

    /// <summary>
    /// Rewired's id for the action.
    /// </summary>
    public int ActionId { get; }

    public float Before { get; }

    public float Now { get; }

    /// <summary>
    /// What one of the game's fields for the action reads this frame, as the game fills it from Rewired's events. Several
    /// can share one action, such as one for each way of steering.
    /// </summary>
    /// <param name="useAsButton">Whether it reads the action as a button, so its axis is 0 or 1.</param>
    /// <param name="useNegativeInput">Whether its button is the action's negative side.</param>
    public ActionFields For(bool useAsButton, bool useNegativeInput)
    {
        var positiveHeld = Now > 0;
        var negativeHeld = Now < 0;
        var positiveDown = positiveHeld && !(Before > 0);
        var negativeDown = negativeHeld && !(Before < 0);
        var positiveUp = !positiveHeld && Before > 0;
        var negativeUp = !negativeHeld && Before < 0;
        var held = useNegativeInput ? negativeHeld : positiveHeld;

        return new ActionFields
        {
            ButtonDown = useNegativeInput ? negativeDown : positiveDown,
            ButtonUp = useNegativeInput ? negativeUp : positiveUp,
            ButtonHeld = held,
            NegativeButtonDown = negativeDown,
            NegativeButtonUp = negativeUp,
            NegativeButtonHeld = negativeHeld,
            PositiveButtonDown = positiveDown,
            PositiveButtonUp = positiveUp,
            PositiveButtonHeld = positiveHeld,
            Axis = useAsButton ? (held ? 1 : 0) : Now,
        };
    }
}
