namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Hands the game's input actions a value by their name, so the keys they're bound to don't matter. <see cref="TesterInput"/>
/// decides what goes in which frame.
/// </summary>
internal interface IActionDriver
{
    /// <summary>
    /// Holds the action at the value from the next frame on, until it's set again or the game turns it off; 0 lets go of
    /// it. Throws a <see cref="Sdk.StepFailedException"/> for an action the game doesn't have, or has turned off, as it
    /// does while a text field has focus, so a step can't press what a player couldn't.
    /// </summary>
    /// <param name="action">The action's name, such as <c>EditorUndo</c>.</param>
    /// <param name="value">1 for a button, or from -1 to 1 for an axis.</param>
    /// <returns>Whether there was anything to take it, as there isn't before the game's input is ready.</returns>
    public bool Set(string action, float value);

    /// <summary>
    /// Lets go of every action it holds. This can't fail. One that throws anyway breaks only itself:
    /// <see cref="TesterInput"/> logs what it threw.
    /// </summary>
    public void GiveBack();
}
