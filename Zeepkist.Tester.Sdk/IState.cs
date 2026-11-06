namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Something that holds in the game or doesn't, such as the Script window being open. <see cref="StateAttribute"/> describes it.
/// </summary>
public interface IState
{
    /// <summary>
    /// Whether the state holds right now. The harness asks this before each step that requires the state or one within it.
    /// </summary>
    public bool IsMet(IStepContext context);
}
