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
}
