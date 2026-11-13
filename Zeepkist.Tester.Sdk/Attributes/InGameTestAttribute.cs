using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Makes a method of a test plugin a C# test: <c>public IEnumerator Name(IStepContext context)</c> on a class with a
/// public constructor that takes nothing. The results name it <c>Class.Method</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class InGameTestAttribute : Attribute
{
    /// <summary>
    /// Where the test starts, as a scenario's <c>start:</c>. The game gets there the way a player does before each test.
    /// </summary>
    public Start Start { get; set; } = Start.Editor;

    /// <summary>
    /// With <see cref="Sdk.Start.Editor"/>, the level file to open rather than a new level, as a scenario's
    /// <c>start: editor: { level: … }</c>. It's relative to the <c>scenarios</c> folder next to the tester, where run.json's
    /// scenario files are, with forward slashes.
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// With <see cref="Sdk.Start.FreePlay"/>, which it needs, the UID of the level to play, as a scenario's
    /// <c>start: free-play: { uid: … }</c>.
    /// </summary>
    public string FreePlayUid { get; set; }
}
