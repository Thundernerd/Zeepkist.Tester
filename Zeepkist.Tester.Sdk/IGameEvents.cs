namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Counts the moments a step waits for, so a wait compares a count from before with the count now.
/// </summary>
public interface IGameEvents
{
    public int ScenesLoaded { get; }

    public int EditorEntries { get; }

    public int TestModeEntries { get; }

    /// <summary>
    /// Counts each release of the soapbox at the start of a round, in test mode as anywhere else.
    /// </summary>
    public int RoundStarts { get; }

    /// <summary>
    /// Counts levels the editor started loading from a file, which it does when it comes back from test mode.
    /// </summary>
    public int LevelLoads { get; }
}
